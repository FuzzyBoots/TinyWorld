using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class PlaceBlock : MonoBehaviour
{
    [SerializeField] GameObject[] _blockList;
    [SerializeField] GameObject _blockPrefab; // Assign your block prefab in the Inspector
    [SerializeField] LayerMask _groundLayerMask; // Set this to your ground layer
    [SerializeField] LayerMask _obstacleLayerMask; // Set this to layers containing objects to check against
    [SerializeField] float _checkIncrement = 0.1f; // How much to raise the block if it's intersecting
    [SerializeField] float _maxCheckHeight = 5f; // Maximum height to check for non-intersection
    [SerializeField] private float _rotateAmount = 45f;

    private GameObject _currentBlock;
    private Renderer _currentBlockRenderer; // To get bounds
    private GameObject[] _shuffledList;
    [SerializeField] private int _blockIndex = 0;

    private void Start()
    {
        _shuffledList = new GameObject[_blockList.Length];
        Array.Copy(_blockList, _shuffledList, _blockList.Length);
        Shuffle(_shuffledList);
        _blockPrefab = _shuffledList[_blockIndex];
    }

    private void Shuffle(GameObject[] shuffledList)
    {
        GameObject swap;
        for (int i=0; i < shuffledList.Length-1; i++)
        {
            int randomIndex = Random.Range(i + 1, shuffledList.Length - 1);
            swap = shuffledList[i];
            shuffledList[i] = shuffledList[randomIndex];
            shuffledList[randomIndex] = swap;
        }
    }

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero); // Assumes ground is at Y=0

        HandleKeyCommands();

        if (groundPlane.Raycast(ray, out float distanceToGround))
        {
            Vector3 targetPosition = ray.GetPoint(distanceToGround);

            if (_currentBlock == null && _blockPrefab != null)
            {
                _currentBlock = Instantiate(_blockPrefab);
                // Disable colliders and rigidbody?
                foreach(Collider coll in _currentBlock.GetComponents<Collider>())
                {
                    coll.enabled = false;
                }
                _currentBlockRenderer = _blockPrefab.GetComponentInChildren<Renderer>();
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
                while (Physics.CheckBox(finalPosition, halfExtents, _currentBlock.transform.rotation, _obstacleLayerMask, QueryTriggerInteraction.Ignore) && currentCheckHeight < _maxCheckHeight)
                {
                    finalPosition.y += _checkIncrement;
                    currentCheckHeight += _checkIncrement;
                }

                if (currentCheckHeight >= _maxCheckHeight)
                {
                    Debug.LogWarning("Could not find a non-intersecting position within maxCheckHeight.");
                    // Optionally, handle this case (e.g., don't place, show an error)
                    // For now, it will place at the max checked height if still intersecting
                }

                _currentBlock.transform.position = finalPosition;

                if (Input.GetMouseButtonDown(0)) // Left click to place
                {
                    // Final check before placing definitively
                    if (Physics.CheckBox(finalPosition, halfExtents, _currentBlock.transform.rotation, _obstacleLayerMask, QueryTriggerInteraction.Ignore))
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
                        Destroy(_currentBlock);
                        //_blockIndex++;
                        //if (_blockIndex > _shuffledList.Length-1)
                        //{
                        //    Shuffle(_shuffledList);
                        //    _blockIndex = 0;
                        //}
                        //_blockPrefab = _shuffledList[_blockIndex];
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

    private void HandleKeyCommands()
    {
        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            _blockIndex--;
            if (_blockIndex < 0 )
            {
                _blockIndex += _blockList.Length;
            }
            _blockPrefab = _blockList[_blockIndex];

            Destroy(_currentBlock);
        }
        else if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            _blockIndex++;
            if (_blockIndex >= _blockList.Length)
            {
                _blockIndex -= _blockList.Length;
            }
            _blockPrefab = _blockList[_blockIndex];

            Destroy(_currentBlock);
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            _blockPrefab.transform.Rotate(0, _rotateAmount, 0);
            _currentBlock.transform.Rotate(0, _rotateAmount, 0);
        } else if (Input.GetKeyDown(KeyCode.R))
        {
            _blockPrefab.transform.Rotate(0, -_rotateAmount, 0);
            _currentBlock.transform.Rotate(0, -_rotateAmount, 0);
        }
    }

    void PlaceFinalBlock(Vector3 position)
    {
        if (_blockPrefab != null)
        {
            GameObject placedBlock = Instantiate(_blockPrefab, position, _currentBlock != null ? _currentBlock.transform.rotation : Quaternion.identity, transform);
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