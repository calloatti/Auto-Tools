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

    // NEW: Cache the ID and the Item Count for each container
    private readonly Dictionary<GameObject, (int id, int count)> _networkInfo = new Dictionary<GameObject, (int id, int count)>();

    // Golden Ratio Tracking (Now using an int ID to prevent memory leaks)
    private readonly Dictionary<int, int> _partitionIndices = new Dictionary<int, int>();
    private int _nextPartitionIndex = 0;

    private void InitializeVisuals()
    {
      _masterContainer = new GameObject("AutoMap_MasterContainer");
      _masterContainer.SetActive(false);

      _lineMaterial = new Material(Shader.Find("Sprites/Default"));
      _lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
      _lineMaterial.renderQueue = 4000;
    }

    // A unified approach: Pre-calculate everything and sort it into cached containers
    private void RebuildAllLines()
    {
      ClearLines();
      HashSet<Automator> visited = new HashSet<Automator>();

      foreach (Automator automator in _automatorRegistry.Transmitters)
      {
        // Skip if this automator was already processed as part of another network
        if (visited.Contains(automator)) continue;

        // Traverse to find the whole interconnected partition
        HashSet<Automator> network = GetConnectedPartition(automator);

        // Create a dedicated parent container for this specific partition
        GameObject networkContainer = new GameObject("NetworkContainer");
        networkContainer.transform.SetParent(_masterContainer.transform);
        _networkContainers.Add(networkContainer);

        // Store the ID and the Count for the notification system
        int partitionId = GetPartitionId(automator.Partition);
        _networkInfo[networkContainer] = (partitionId, network.Count);

        // Map every building in this partition to this container for O(1) lookups later
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
    }

    // Modified to return the active GameObject so the Input script can read it
    public GameObject ShowOnlyPartition(Automator selectedAutomator)
    {
      // Find which container this building belongs to
      _automatorToNetwork.TryGetValue(selectedAutomator, out GameObject activeContainer);

      // Instantly toggle GameObjects based on the selection
      foreach (GameObject container in _networkContainers)
      {
        if (container != null)
        {
          container.SetActive(container == activeContainer);
        }
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

    // Extracted the ID generation so we can use it for both colors and notifications
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

      // Golden ratio conjugate to maximize hue spacing
      float goldenRatioConjugate = 0.618033988749895f;
      float hue = (index * goldenRatioConjugate) % 1f;

      // High saturation (0.85) and value (0.95) to keep lines bright and distinct
      return Color.HSVToRGB(hue, 0.85f, 0.95f);
    }

    private void DrawTransmitterConnections(Automator transmitter, Transform parentContainer)
    {
      if (transmitter.OutputConnections.Count == 0) return;

      // Fetch the color based on the partition rather than individual coordinates
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
      GameObject lineObj = new GameObject("AutoLine");
      lineObj.transform.SetParent(parentContainer); // Bind to the specific partition's container

      LineRenderer lr = lineObj.AddComponent<LineRenderer>();
      lr.material = _lineMaterial;

      // Thinner lines
      lr.startWidth = 0.05f;
      lr.endWidth = 0.05f;

      lr.useWorldSpace = true;
      lr.sortingOrder = 32767;

      // Constant color and 100% solid (alpha 1.0) the entire way
      Gradient gradient = new Gradient();
      gradient.SetKeys(
          new GradientColorKey[] { new GradientColorKey(color, 0.0f), new GradientColorKey(color, 1.0f) },
          new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
      );
      lr.colorGradient = gradient;

      float distance = Vector3.Distance(start, end);

      // Deterministic random offset (only up) for short distances
      int seed = unchecked(start.GetHashCode() ^ (end.GetHashCode() * 397));
      var oldState = UnityEngine.Random.state;
      UnityEngine.Random.InitState(seed);
      float randomHeightBoost = UnityEngine.Random.Range(0.0f, 1.5f);
      UnityEngine.Random.state = oldState;

      // Base height 1.2 gets the random boost. Long distances will naturally override this.
      float arcHeight = Mathf.Max(1.2f + randomHeightBoost, distance * 0.2f);

      lr.positionCount = 21;

      // Calculate Bezier control points for a steeper, non-vertical approach angle
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

        // Apply Cubic Bezier formula
        Vector3 pos = uuu * p0;
        pos += 3f * uu * t * p1;
        pos += 3f * u * tt * p2;
        pos += ttt * p3;

        lr.SetPosition(i, pos);
      }
    }

    public Vector3 GetCenterPosition(Automator automator)
    {
      // 1. Get the official game-provided building center
      var centerComponent = automator.GetComponent<BlockObjectCenter>();
      Vector3 pos = (centerComponent != null)
          ? centerComponent.WorldCenterAtBaseZ
          : automator.GameObject.transform.position;

      // 3. Add exactly 1 meter higher, as requested
      pos.y += 0.5f;

      return pos;
    }

    private void ClearLines()
    {
      foreach (GameObject obj in _networkContainers) if (obj != null) UnityEngine.Object.Destroy(obj);
      _networkContainers.Clear();
      _automatorToNetwork.Clear();
      _networkInfo.Clear(); // Clear the info cache too
    }

    private void SetVisibility(bool visible)
    {
      if (_masterContainer != null) _masterContainer.SetActive(visible);
    }

    private void OnDispose()
    {
      ClearLines();

      // Clear out references so the Garbage Collector can delete old partitions
      _partitionIndices.Clear();
      _nextPartitionIndex = 0;

      if (_masterContainer != null) UnityEngine.Object.Destroy(_masterContainer);
      if (_lineMaterial != null) UnityEngine.Object.Destroy(_lineMaterial);
    }
  }
}