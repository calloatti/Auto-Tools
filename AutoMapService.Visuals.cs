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

    // Caching structural components
    private readonly List<GameObject> _networkContainers = new List<GameObject>();
    private readonly Dictionary<Automator, GameObject> _automatorToNetwork = new Dictionary<Automator, GameObject>();
    private readonly Dictionary<GameObject, (int id, int count)> _networkInfo = new Dictionary<GameObject, (int id, int count)>();

    // Optimization: Object Pooling to prevent GC spikes from Instantiate/Destroy
    private readonly Queue<GameObject> _containerPool = new Queue<GameObject>();
    private readonly Queue<LineRenderer> _linePool = new Queue<LineRenderer>();
    private readonly List<LineRenderer> _activeLines = new List<LineRenderer>();

    // Optimization: O(1) Active Container Lookup
    private GameObject _currentlyActivePartitionContainer = null;

    // Optimization: Reusable array for Bezier curve points to eliminate array allocation GC
    private readonly Vector3[] _bezierPointsCache = new Vector3[21];

    // Golden Ratio Tracking
    private readonly Dictionary<int, int> _partitionIndices = new Dictionary<int, int>();
    private int _nextPartitionIndex = 0;

    // Tunable height fraction for connection points (0.75 = 3/4 height, 0.80 = 4/5 height)
    private float _connectionHeightFraction = 0.75f;

    private void InitializeVisuals()
    {
      _masterContainer = new GameObject("AutoMap_MasterContainer");
      _masterContainer.SetActive(false);

      _lineMaterial = new Material(Shader.Find("Sprites/Default"));
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

        // Fetch a container from the pool instead of creating a new one
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

    public void SetAllPartitionsActive(bool active)
    {
      foreach (GameObject container in _networkContainers)
      {
        if (container != null) container.SetActive(active);
      }

      // Resetting this to null acts as our trigger to sweep off all containers 
      // the next time we enter Partition mode.
      _currentlyActivePartitionContainer = null;
    }

    public GameObject ShowOnlyPartition(Automator selectedAutomator)
    {
      _automatorToNetwork.TryGetValue(selectedAutomator, out GameObject activeContainer);

      // O(1) Fast Exit: If we are already looking at this container, do nothing
      if (_currentlyActivePartitionContainer == activeContainer)
      {
        return activeContainer;
      }

      // If cache is null, we just entered from Global mode where everything was ON.
      // Sweep everything OFF once.
      if (_currentlyActivePartitionContainer == null)
      {
        foreach (GameObject container in _networkContainers)
        {
          if (container != null) container.SetActive(false);
        }
      }
      else
      {
        // Otherwise, just turn off the previously active container (O(1))
        _currentlyActivePartitionContainer.SetActive(false);
      }

      // Turn on the newly selected container
      if (activeContainer != null)
      {
        activeContainer.SetActive(true);
      }

      _currentlyActivePartitionContainer = activeContainer;
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
        {
          if (connection.Receiver != null && network.Add(connection.Receiver)) queue.Enqueue(connection.Receiver);
        }
        foreach (AutomatorConnection connection in current.InputConnections)
        {
          if (connection.Transmitter != null && network.Add(connection.Transmitter)) queue.Enqueue(connection.Transmitter);
        }
      }
      return network;
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

      foreach (AutomatorConnection connection in transmitter.OutputConnections)
      {
        if (connection.Receiver == null) continue;
        CreateLine(startPos, GetCenterPosition(connection.Receiver), baseColor, parentContainer);
      }
    }

    private void CreateLine(Vector3 start, Vector3 end, Color color, Transform parentContainer)
    {
      // Fetch a line from the pool instead of Instantiating
      LineRenderer lr = GetPooledLine(parentContainer);
      _activeLines.Add(lr);

      Gradient gradient = new Gradient();
      gradient.SetKeys(
          new GradientColorKey[] { new GradientColorKey(color, 0.0f), new GradientColorKey(color, 1.0f) },
          new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
      );
      lr.colorGradient = gradient;

      float distance = Vector3.Distance(start, end);

      int seed = unchecked(start.GetHashCode() ^ (end.GetHashCode() * 397));
      var oldState = UnityEngine.Random.state;
      UnityEngine.Random.InitState(seed);
      float randomHeightBoost = UnityEngine.Random.Range(0.0f, 1.5f);
      UnityEngine.Random.state = oldState;

      float arcHeight = Mathf.Max(1.2f + randomHeightBoost, distance * 0.2f);

      Vector3 p0 = start;
      Vector3 p3 = end;

      Vector3 p1 = Vector3.Lerp(start, end, 0.10f);
      p1.y += arcHeight * 1.333f;

      Vector3 p2 = Vector3.Lerp(start, end, 0.40f);
      p2.y += arcHeight * 1.333f;

      // Write positions to the reusable array cache
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

      // Update LineRenderer in a single batch call to GPU
      lr.positionCount = 21;
      lr.SetPositions(_bezierPointsCache);
    }

    public Vector3 GetCenterPosition(Automator automator)
    {
      var centerComponent = automator.GetComponent<BlockObjectCenter>();

      if (centerComponent == null)
      {
        return automator.GameObject.transform.position + new Vector3(0, 0.5f, 0);
      }

      float bottomY = centerComponent.WorldCenterAtBaseZ.y;
      float middleY = centerComponent.WorldCenter.y;

      float halfHeight = middleY - bottomY;
      float totalHeight = halfHeight * 2f;

      Vector3 pos = centerComponent.WorldCenterAtBaseZ;
      pos.y = bottomY + (totalHeight * _connectionHeightFraction);

      return pos;
    }

    // --- OBJECT POOLING METHODS ---

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

    private LineRenderer GetPooledLine(Transform parentContainer)
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
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.useWorldSpace = true;
        lr.sortingOrder = 32767;
      }
      return lr;
    }

    private void ClearLines()
    {
      // Return all active lines to the pool
      foreach (LineRenderer lr in _activeLines)
      {
        if (lr != null)
        {
          lr.gameObject.SetActive(false);
          lr.transform.SetParent(_masterContainer.transform); // Re-bind to master while inactive
          _linePool.Enqueue(lr);
        }
      }
      _activeLines.Clear();

      // Return all active containers to the pool
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
      _currentlyActivePartitionContainer = null;
    }

    private void SetVisibility(bool visible)
    {
      if (_masterContainer != null) _masterContainer.SetActive(visible);
    }

    private void OnDispose()
    {
      ClearLines();

      _partitionIndices.Clear();
      _nextPartitionIndex = 0;

      // Clean up pooled GameObjects to prevent memory leaks when mod unloads
      foreach (GameObject obj in _containerPool) if (obj != null) UnityEngine.Object.Destroy(obj);
      foreach (LineRenderer lr in _linePool) if (lr != null) UnityEngine.Object.Destroy(lr.gameObject);

      _containerPool.Clear();
      _linePool.Clear();

      if (_masterContainer != null) UnityEngine.Object.Destroy(_masterContainer);
      if (_lineMaterial != null) UnityEngine.Object.Destroy(_lineMaterial);
    }
  }
}