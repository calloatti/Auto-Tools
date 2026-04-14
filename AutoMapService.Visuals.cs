using System.Collections.Generic;
using Timberborn.Automation;
using Timberborn.BlockObjectModelSystem;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using UnityEngine;
using UnityEngine.Rendering;

namespace Calloatti.AutoTools
{
  public partial class AutoMapService
  {
    private GameObject _masterContainer;
    private Texture2D _glowTexture;

    private readonly List<GameObject> _networkContainers = new List<GameObject>();
    private readonly Dictionary<Automator, GameObject> _automatorToNetwork = new Dictionary<Automator, GameObject>();
    private readonly Dictionary<GameObject, (int id, int count)> _networkInfo = new Dictionary<GameObject, (int id, int count)>();
    private readonly Dictionary<Automator, List<LineRenderer>> _automatorToLines = new Dictionary<Automator, List<LineRenderer>>();
    private readonly Dictionary<LineRenderer, LineRenderer> _coreToGlowMap = new Dictionary<LineRenderer, LineRenderer>();

    private readonly Queue<GameObject> _containerPool = new Queue<GameObject>();
    private readonly Queue<LineRenderer> _linePool = new Queue<LineRenderer>();
    private readonly List<LineRenderer> _activeLines = new List<LineRenderer>();

    private GameObject _currentlyActivePartitionContainer = null;
    private readonly Vector3[] _bezierPointsCache = new Vector3[21];

    private readonly Dictionary<int, int> _partitionIndices = new Dictionary<int, int>();
    private int _nextPartitionIndex = 0;

    // --- CONFIGURABLE VISUAL VARIABLES ---
    private float _connectionHeightFraction;
    private float _glowWidthMultiplier;
    private float _glowAlpha;
    private float _offStateBrightnessMultiplier;

    private void InitializeVisuals()
    {
      _masterContainer = new GameObject("AutoMap_MasterContainer");
      _masterContainer.SetActive(false);

      _glowTexture = new Texture2D(1, 64);
      _glowTexture.wrapMode = TextureWrapMode.Clamp;
      for (int i = 0; i < 64; i++)
      {
        float distFromCenter = Mathf.Abs((i / 63f) - 0.5f) * 2f;
        float alpha = Mathf.Clamp01(1f - (distFromCenter * distFromCenter));
        _glowTexture.SetPixel(0, i, new Color(1, 1, 1, alpha));
      }
      _glowTexture.Apply();

      _lineMaterial = new Material(Shader.Find("Sprites/Default"));
      _lineMaterial.mainTexture = _glowTexture;
      _lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
      _lineMaterial.renderQueue = 4000;
    }

    private void RebuildAllLines()
    {
      ClearLines();
      HashSet<Automator> visited = new HashSet<Automator>();

      foreach (Automator automator in _automatorRegistry.Transmitters)
      {
        if (visited.Contains(automator)) continue;

        HashSet<Automator> network = GetConnectedPartition(automator);
        GameObject networkContainer = GetPooledContainer();
        _networkContainers.Add(networkContainer);

        int partitionId = GetPartitionId(automator.Partition);
        _networkInfo[networkContainer] = (partitionId, network.Count);

        foreach (Automator member in network)
        {
          visited.Add(member);
          _automatorToNetwork[member] = networkContainer;
          if (member.IsTransmitter) DrawTransmitterConnections(member, networkContainer.transform);
        }
      }
    }

    public int GetPartitionId(AutomatorPartition partition)
    {
      if (partition == null) return -1;
      int partitionId = partition.GetHashCode();
      if (!_partitionIndices.TryGetValue(partitionId, out int index))
      {
        index = _nextPartitionIndex++;
        _partitionIndices[partitionId] = index;
      }
      return index;
    }

    public Color GetPartitionColor(AutomatorPartition partition)
    {
      int index = GetPartitionId(partition);
      if (index == -1) return Color.white;
      float goldenRatioConjugate = 0.618033988749895f;
      float hue = (index * goldenRatioConjugate) % 1f;
      return Color.HSVToRGB(hue, 0.85f, 0.95f);
    }

    private void DrawTransmitterConnections(Automator transmitter, Transform parentContainer)
    {
      if (transmitter.OutputConnections.Count == 0) return;
      Color baseColor = GetPartitionColor(transmitter.Partition);
      Vector3 startPos = GetCenterPosition(transmitter);
      List<LineRenderer> coreLines = new List<LineRenderer>();

      foreach (AutomatorConnection connection in transmitter.OutputConnections)
      {
        if (connection.Receiver == null) continue;

        LineRenderer core = GetPooledLine(parentContainer, isGlow: false);
        core.SetPositions(CalculateBezier(startPos, GetCenterPosition(connection.Receiver)));
        _activeLines.Add(core);
        coreLines.Add(core);

        LineRenderer glow = GetPooledLine(parentContainer, isGlow: true);
        glow.SetPositions(_bezierPointsCache);
        _coreToGlowMap[core] = glow;
      }

      _automatorToLines[transmitter] = coreLines;
      UpdateLineColors(transmitter);
    }

    public void UpdateLineColors(Automator automator)
    {
      if (_currentState == MapDisplayState.Hidden) return;
      if (!_automatorToLines.TryGetValue(automator, out List<LineRenderer> lines)) return;

      Color baseColor = GetPartitionColor(automator.Partition);
      bool isOn = automator.State == AutomatorState.On;

      foreach (LineRenderer core in lines)
      {
        if (core == null) continue;

        Color coreColor = baseColor;
        if (!isOn)
        {
          Color.RGBToHSV(baseColor, out float h, out float s, out float v);
          coreColor = Color.HSVToRGB(h, s, v * _offStateBrightnessMultiplier);
        }

        Gradient coreGrad = new Gradient();
        coreGrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(coreColor, 0.0f), new GradientColorKey(coreColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
        );
        core.colorGradient = coreGrad;

        if (_coreToGlowMap.TryGetValue(core, out LineRenderer glow))
        {
          glow.gameObject.SetActive(isOn);
          if (isOn)
          {
            Gradient glowGrad = new Gradient();
            glowGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(baseColor, 0.0f), new GradientColorKey(baseColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(_glowAlpha, 0.0f), new GradientAlphaKey(_glowAlpha, 1.0f) }
            );
            glow.colorGradient = glowGrad;
          }
        }
      }
    }

    private Vector3[] CalculateBezier(Vector3 start, Vector3 end)
    {
      float distance = Vector3.Distance(start, end);
      int seed = unchecked(start.GetHashCode() ^ (end.GetHashCode() * 397));
      var oldState = Random.state;
      Random.InitState(seed);
      float randomHeightBoost = Random.Range(0.0f, 1.5f);
      Random.state = oldState;

      float arcHeight = Mathf.Max(1.2f + randomHeightBoost, distance * 0.2f);
      Vector3 p0 = start;
      Vector3 p3 = end;
      Vector3 p1 = Vector3.Lerp(start, end, 0.10f);
      p1.y += arcHeight * 1.333f;
      Vector3 p2 = Vector3.Lerp(start, end, 0.40f);
      p2.y += arcHeight * 1.333f;

      for (int i = 0; i <= 20; i++)
      {
        float t = i / 20f;
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 pos = uuu * p0;
        pos += 3f * uu * t * p1;
        pos += 3f * u * tt * p2;
        pos += ttt * p3;
        _bezierPointsCache[i] = pos;
      }
      return _bezierPointsCache;
    }

    public void SetAllPartitionsActive(bool active)
    {
      foreach (GameObject container in _networkContainers)
      {
        if (container != null) container.SetActive(active);
      }
      _currentlyActivePartitionContainer = null;
    }

    public GameObject ShowOnlyPartition(Automator selectedAutomator)
    {
      _automatorToNetwork.TryGetValue(selectedAutomator, out GameObject activeContainer);

      // FIX: Force hide all partitions if we just rebuilt them (_currentlyActivePartitionContainer == null),
      // even if the newly selected automator is completely isolated (activeContainer == null).
      if (_currentlyActivePartitionContainer != activeContainer || _currentlyActivePartitionContainer == null)
      {
        if (_currentlyActivePartitionContainer == null)
        {
          foreach (GameObject container in _networkContainers)
          {
            if (container != null) container.SetActive(false);
          }
        }
        else
        {
          _currentlyActivePartitionContainer.SetActive(false);
        }

        if (activeContainer != null)
        {
          activeContainer.SetActive(true);
        }

        _currentlyActivePartitionContainer = activeContainer;
      }

      return activeContainer;
    }

    private HashSet<Automator> GetConnectedPartition(Automator startNode)
    {
      HashSet<Automator> network = new HashSet<Automator>();
      Queue<Automator> queue = new Queue<Automator>();
      queue.Enqueue(startNode);
      network.Add(startNode);
      while (queue.Count > 0)
      {
        Automator current = queue.Dequeue();
        foreach (AutomatorConnection connection in current.OutputConnections)
          if (connection.Receiver != null && network.Add(connection.Receiver)) queue.Enqueue(connection.Receiver);
        foreach (AutomatorConnection connection in current.InputConnections)
          if (connection.Transmitter != null && network.Add(connection.Transmitter)) queue.Enqueue(connection.Transmitter);
      }
      return network;
    }

    public Vector3 GetCenterPosition(Automator automator)
    {
      var centerComponent = automator.GetComponent<BlockObjectCenter>();
      if (centerComponent == null) return automator.GameObject.transform.position + new Vector3(0, 0.5f, 0);
      float bottomY = centerComponent.WorldCenterAtBaseZ.y;
      float middleY = centerComponent.WorldCenter.y;
      float halfHeight = middleY - bottomY;
      float totalHeight = halfHeight * 2f;
      Vector3 pos = centerComponent.WorldCenterAtBaseZ;
      pos.y = bottomY + (totalHeight * _connectionHeightFraction);
      return pos;
    }

    private GameObject GetPooledContainer()
    {
      if (_containerPool.Count > 0)
      {
        GameObject container = _containerPool.Dequeue();
        container.SetActive(true);
        return container;
      }
      GameObject newContainer = new GameObject("NetworkContainer");
      newContainer.transform.SetParent(_masterContainer.transform);
      return newContainer;
    }

    private LineRenderer GetPooledLine(Transform parentContainer, bool isGlow)
    {
      LineRenderer lr;
      if (_linePool.Count > 0)
      {
        lr = _linePool.Dequeue();
        lr.transform.SetParent(parentContainer);
        lr.gameObject.SetActive(true);
      }
      else
      {
        GameObject lineObj = new GameObject("AutoLine");
        lineObj.transform.SetParent(parentContainer);
        lr = lineObj.AddComponent<LineRenderer>();
        lr.material = _lineMaterial;
        lr.useWorldSpace = true;
        lr.sortingOrder = 32767;
      }

      if (isGlow)
      {
        float width = 0.05f * _glowWidthMultiplier;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.gameObject.name = "GlowRibbon";
      }
      else
      {
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.gameObject.name = "CoreLine";
      }
      lr.positionCount = 21;
      return lr;
    }

    private void ClearLines()
    {
      foreach (LineRenderer lr in _activeLines)
      {
        if (lr != null)
        {
          lr.gameObject.SetActive(false);
          lr.transform.SetParent(_masterContainer.transform);
          _linePool.Enqueue(lr);
          if (_coreToGlowMap.TryGetValue(lr, out LineRenderer glow))
          {
            glow.gameObject.SetActive(false);
            glow.transform.SetParent(_masterContainer.transform);
            _linePool.Enqueue(glow);
          }
        }
      }
      _activeLines.Clear();
      _coreToGlowMap.Clear();
      foreach (GameObject container in _networkContainers)
      {
        if (container != null)
        {
          container.SetActive(false);
          _containerPool.Enqueue(container);
        }
      }
      _networkContainers.Clear();
      _automatorToNetwork.Clear();
      _networkInfo.Clear();
      _automatorToLines.Clear();
      _currentlyActivePartitionContainer = null;
    }

    private void SetVisibility(bool visible) => _masterContainer?.SetActive(visible);

    private void OnDispose()
    {
      ClearLines();
      _partitionIndices.Clear();
      _nextPartitionIndex = 0;
      foreach (GameObject obj in _containerPool) if (obj != null) Object.Destroy(obj);
      foreach (LineRenderer lr in _linePool) if (lr != null) Object.Destroy(lr.gameObject);
      _containerPool.Clear();
      _linePool.Clear();
      if (_masterContainer != null) Object.Destroy(_masterContainer);
      if (_lineMaterial != null) Object.Destroy(_lineMaterial);
      if (_glowTexture != null) Object.Destroy(_glowTexture);
    }
  }
}