using UnityEngine;

public class PlaceBlock : MonoBehaviour
{
    public GameObject blockPrefab; // Assign your block prefab in the Inspector
    public LayerMask groundLayerMask; // Set this to your ground layer
    public LayerMask obstacleLayerMask; // Set this to layers containing objects to check against
    public float checkIncrement = 0.1f; // How much to raise the block if it's intersecting
    public float maxCheckHeight = 5f; // Maximum height to check for non-intersection

    private GameObject _currentBlock;
    private Renderer _currentBlockRenderer; // To get bounds

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero); // Assumes ground is at Y=0

        if (groundPlane.Raycast(ray, out float distanceToGround))
        {
            Vector3 targetPosition = ray.GetPoint(distanceToGround);

            if (_currentBlock == null && blockPrefab != null)
            {
                _currentBlock = Instantiate(blockPrefab);
                _currentBlockRenderer = _currentBlock.GetComponentInChildren<Renderer>();
                if (_currentBlockRenderer == null)
                {
                    Debug.LogError("Block prefab needs a Renderer component in itself or children to determine bounds.");
                    Destroy(_currentBlock);
                    enabled = false; // Disable script if no renderer
                    return;
                }
            }

            if (_currentBlock != null && _currentBlockRenderer != null)
            {
                Bounds blockBounds = _currentBlockRenderer.bounds;
                Vector3 halfExtents = blockBounds.extents;
                // Adjust targetPosition so the bottom of the block is on the ground initially
                Vector3 finalPosition = targetPosition + Vector3.up * halfExtents.y;

                // Iteratively check for collisions and move up
                float currentCheckHeight = 0f;
                while (Physics.CheckBox(finalPosition, halfExtents, _currentBlock.transform.rotation, obstacleLayerMask, QueryTriggerInteraction.Ignore) && currentCheckHeight < maxCheckHeight)
                {
                    finalPosition.y += checkIncrement;
                    currentCheckHeight += checkIncrement;
                }

                if (currentCheckHeight >= maxCheckHeight)
                {
                    Debug.LogWarning("Could not find a non-intersecting position within maxCheckHeight.");
                    // Optionally, handle this case (e.g., don't place, show an error)
                    // For now, it will place at the max checked height if still intersecting
                }

                _currentBlock.transform.position = finalPosition;

                if (Input.GetMouseButtonDown(0)) // Left click to place
                {
                    // Final check before placing definitively
                    if (Physics.CheckBox(finalPosition, halfExtents, _currentBlock.transform.rotation, obstacleLayerMask, QueryTriggerInteraction.Ignore))
                    {
                        Debug.LogWarning("Final placement position is still intersecting. Consider increasing checkIncrement or maxCheckHeight, or disallow placement.");
                        // Destroy(_currentBlock); // Optionally destroy if still intersecting
                        // _currentBlock = null;
                        // _currentBlockRenderer = null;

                    }
                    else
                    {
                        // Create a new instance for the next block, detaching the current one
                        PlaceFinalBlock(finalPosition);
                        _currentBlock = null; // Ready to spawn a new preview block
                        _currentBlockRenderer = null;
                    }
                }
            }
        }
        else
        {
            // If ray doesn't hit ground, optionally hide or destroy preview block
            if (_currentBlock != null)
            {
                Destroy(_currentBlock);
                _currentBlock = null;
                _currentBlockRenderer = null;
            }
        }
    }

    void PlaceFinalBlock(Vector3 position)
    {
        if (blockPrefab != null)
        {
            GameObject placedBlock = Instantiate(blockPrefab, position, _currentBlock != null ? _currentBlock.transform.rotation : Quaternion.identity);
            // Add any other logic for a placed block here (e.g., removing this script from it)
            Debug.Log($"Block placed at {placedBlock.transform.position}");
        }
    }

    // Optional: Visualize the CheckBox in the editor
    void OnDrawGizmos()
    {
        if (_currentBlock != null && _currentBlockRenderer != null)
        {
            Gizmos.color = Color.red;
            Bounds blockBounds = _currentBlockRenderer.bounds;
            Gizmos.DrawWireCube(_currentBlock.transform.position, blockBounds.size);
        }
    }
}