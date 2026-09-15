using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InfiniteBackgroundTerrain : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform[] terrainLayers;
    [SerializeField, Min(1)] private int tileRadius = 2;
    [SerializeField, Range(0, 2)] private int forwardBias = 1;
    [SerializeField, Min(0)] private int collisionRadius = 1;
    [SerializeField, Min(0f)] private float minimumDirectionSpeed = 2f;

    private readonly Dictionary<Vector2Int, TerrainTile> m_ActiveTiles = new();
    private readonly Queue<TerrainTile> m_FreeTiles = new();
    private readonly HashSet<Vector2Int> m_RequiredCoordinates = new();
    private readonly List<Vector2Int> m_RecycleCoordinates = new();

    private Rigidbody m_PlayerBody;
    private Vector2 m_TileSize;
    private Vector3 m_BaseBoundsCenter;
    private Vector2Int m_LastGridCenter = new(int.MinValue, int.MinValue);
    private Vector2Int m_LastPlayerCell = new(int.MinValue, int.MinValue);
    private int m_PoolSize;

    private sealed class TerrainTile
    {
        public readonly GameObject Root;
        public ColliderState[] Colliders;

        public TerrainTile(GameObject root)
        {
            Root = root;
        }
    }

    private readonly struct ColliderState
    {
        public readonly Collider Collider;
        public readonly bool OriginallyEnabled;

        public ColliderState(Collider collider)
        {
            Collider = collider;
            OriginallyEnabled = collider.enabled;
        }
    }

    private void Awake()
    {
        if (!ResolvePlayer() || !ValidateTerrainLayers() || !TryGetTerrainBounds(out Bounds bounds))
        {
            enabled = false;
            return;
        }

        m_TileSize = new Vector2(bounds.size.x, bounds.size.z);
        m_BaseBoundsCenter = bounds.center;

        if (m_TileSize.x <= Mathf.Epsilon || m_TileSize.y <= Mathf.Epsilon)
        {
            Debug.LogError("BackgroundTerrain has invalid horizontal bounds and cannot be tiled.", this);
            enabled = false;
            return;
        }

        m_PoolSize = (tileRadius * 2 + 1) * (tileRadius * 2 + 1);
        PrewarmTiles();

        for (int i = 0; i < terrainLayers.Length; i++)
        {
            terrainLayers[i].gameObject.SetActive(false);
        }

        RefreshTiles(force: true);
    }

    private void LateUpdate()
    {
        RefreshTiles(force: false);
    }

    private bool ResolvePlayer()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (player == null)
        {
            Debug.LogError("BackgroundTerrain could not find the player transform.", this);
            return false;
        }

        m_PlayerBody = player.GetComponent<Rigidbody>();
        return true;
    }

    private bool ValidateTerrainLayers()
    {
        if (terrainLayers == null || terrainLayers.Length == 0)
        {
            Debug.LogError("BackgroundTerrain has no terrain layers assigned.", this);
            return false;
        }

        for (int i = 0; i < terrainLayers.Length; i++)
        {
            if (terrainLayers[i] == null)
            {
                Debug.LogError("BackgroundTerrain contains an empty terrain layer reference.", this);
                return false;
            }
        }

        return true;
    }

    private bool TryGetTerrainBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < terrainLayers.Length; i++)
        {
            Renderer[] renderers = terrainLayers[i].GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int j = 0; j < renderers.Length; j++)
            {
                if (!hasBounds)
                {
                    bounds = renderers[j].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[j].bounds);
                }
            }
        }

        if (hasBounds)
        {
            return true;
        }

        for (int i = 0; i < terrainLayers.Length; i++)
        {
            Collider[] colliders = terrainLayers[i].GetComponentsInChildren<Collider>(includeInactive: true);
            for (int j = 0; j < colliders.Length; j++)
            {
                if (!hasBounds)
                {
                    bounds = colliders[j].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(colliders[j].bounds);
                }
            }
        }

        if (!hasBounds)
        {
            Debug.LogError("BackgroundTerrain could not determine the terrain footprint.", this);
        }

        return hasBounds;
    }

    private void PrewarmTiles()
    {
        for (int i = 0; i < m_PoolSize; i++)
        {
            GameObject tileRoot = new GameObject($"BackgroundTerrain_Tile_{i:00}");
            tileRoot.transform.SetParent(transform, worldPositionStays: false);

            for (int layerIndex = 0; layerIndex < terrainLayers.Length; layerIndex++)
            {
                GameObject layerClone = Instantiate<GameObject>(
                    terrainLayers[layerIndex].gameObject,
                    tileRoot.transform,
                    false);

                layerClone.name = terrainLayers[layerIndex].name;
                SetDynamicRecursively(layerClone);
            }

            Collider[] colliders = tileRoot.GetComponentsInChildren<Collider>(includeInactive: true);
            ColliderState[] colliderStates = new ColliderState[colliders.Length];
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                colliderStates[colliderIndex] = new ColliderState(colliders[colliderIndex]);
                colliders[colliderIndex].enabled = false;
            }

            tileRoot.SetActive(false);
            TerrainTile tile = new TerrainTile(tileRoot)
            {
                Colliders = colliderStates
            };
            m_FreeTiles.Enqueue(tile);
        }
    }

    private static void SetDynamicRecursively(GameObject root)
    {
        root.isStatic = false;
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
        {
            SetDynamicRecursively(rootTransform.GetChild(i).gameObject);
        }
    }

    private void RefreshTiles(bool force)
    {
        Vector2Int playerCell = WorldToCell(player.position);
        Vector2Int direction = GetTravelDirection();
        Vector2Int gridCenter = playerCell + direction * forwardBias;

        if (!force && gridCenter == m_LastGridCenter)
        {
            if (playerCell != m_LastPlayerCell)
            {
                m_LastPlayerCell = playerCell;
                UpdateTileColliders(playerCell);
            }
            return;
        }

        m_LastGridCenter = gridCenter;
        m_LastPlayerCell = playerCell;
        m_RequiredCoordinates.Clear();

        for (int x = -tileRadius; x <= tileRadius; x++)
        {
            for (int y = -tileRadius; y <= tileRadius; y++)
            {
                m_RequiredCoordinates.Add(new Vector2Int(gridCenter.x + x, gridCenter.y + y));
            }
        }

        m_RecycleCoordinates.Clear();
        foreach (KeyValuePair<Vector2Int, TerrainTile> entry in m_ActiveTiles)
        {
            if (!m_RequiredCoordinates.Contains(entry.Key))
            {
                m_RecycleCoordinates.Add(entry.Key);
            }
        }

        for (int i = 0; i < m_RecycleCoordinates.Count; i++)
        {
            Vector2Int coordinate = m_RecycleCoordinates[i];
            TerrainTile tile = m_ActiveTiles[coordinate];
            m_ActiveTiles.Remove(coordinate);
            tile.Root.SetActive(false);
            m_FreeTiles.Enqueue(tile);
        }

        foreach (Vector2Int coordinate in m_RequiredCoordinates)
        {
            if (m_ActiveTiles.ContainsKey(coordinate))
            {
                continue;
            }

            if (m_FreeTiles.Count == 0)
            {
                Debug.LogError("BackgroundTerrain tile pool was exhausted.", this);
                return;
            }

            TerrainTile tile = m_FreeTiles.Dequeue();
            PositionTile(tile, coordinate);
            m_ActiveTiles.Add(coordinate, tile);
        }

        UpdateTileColliders(playerCell);
    }

    private void UpdateTileColliders(Vector2Int playerCell)
    {
        foreach (KeyValuePair<Vector2Int, TerrainTile> entry in m_ActiveTiles)
        {
            Vector2Int offset = entry.Key - playerCell;
            bool enabledForPhysics = Mathf.Abs(offset.x) <= collisionRadius && Mathf.Abs(offset.y) <= collisionRadius;
            ColliderState[] colliders = entry.Value.Colliders;

            for (int i = 0; i < colliders.Length; i++)
            {
                ColliderState state = colliders[i];
                if (state.Collider != null)
                {
                    state.Collider.enabled = enabledForPhysics && state.OriginallyEnabled;
                }
            }
        }
    }

    private Vector2Int WorldToCell(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.RoundToInt((worldPosition.x - m_BaseBoundsCenter.x) / m_TileSize.x),
            Mathf.RoundToInt((worldPosition.z - m_BaseBoundsCenter.z) / m_TileSize.y));
    }

    private Vector2Int GetTravelDirection()
    {
        if (m_PlayerBody == null)
        {
            return Vector2Int.zero;
        }

        Vector3 velocity = m_PlayerBody.linearVelocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude < minimumDirectionSpeed * minimumDirectionSpeed)
        {
            return Vector2Int.zero;
        }

        velocity.Normalize();
        int x = Mathf.Abs(velocity.x) >= 0.35f ? (velocity.x > 0f ? 1 : -1) : 0;
        int y = Mathf.Abs(velocity.z) >= 0.35f ? (velocity.z > 0f ? 1 : -1) : 0;
        return new Vector2Int(x, y);
    }

    private void PositionTile(TerrainTile tile, Vector2Int coordinate)
    {
        Vector3 worldOffset = new Vector3(
            coordinate.x * m_TileSize.x,
            0f,
            coordinate.y * m_TileSize.y);

        tile.Root.transform.position = transform.position + worldOffset;
        tile.Root.transform.rotation = transform.rotation;
        tile.Root.SetActive(true);
    }
}
