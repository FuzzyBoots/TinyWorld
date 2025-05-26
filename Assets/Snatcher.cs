using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

[RequireComponent (typeof(NavMeshAgent))]
public class Snatcher : MonoBehaviour
{
    NavMeshAgent _agent;
    [SerializeField] GameObject _target;
    [SerializeField] Vector3 _randomDestination;

    [SerializeField] private float _searchRadius;
    [SerializeField] private int _blockGoal;
    [SerializeField] private float _threshold = 2f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();

        SetRandomLocation();
    }

    private void SetRandomLocation()
    {
        _randomDestination = new Vector3(Random.Range(-25f, 25f), 0, Random.Range(-25f, 25f));
        _agent.SetDestination(_randomDestination);
    }

    // Update is called once per frame
    void Update()
    {
        if (_target != null)
        {
            _agent.SetDestination(_target.transform.position);
        } else { 
            _agent.SetDestination(_randomDestination);
            if (Vector3.Distance(transform.position, _randomDestination) < _threshold)
            {
                SetRandomLocation();
            }

            _target = LocateTarget();
        }
    }

    private GameObject LocateTarget()
    {
        GameObject target = null;
        float closestDistance = Mathf.Infinity;
        Collider[] colliders = Physics.OverlapSphere(transform.position, _searchRadius, LayerMask.GetMask("Block"));
        foreach (Collider collider in colliders)
        {
            GameObject gameObject = collider.gameObject;
            if (TryGetComponent<BlockScript>(out BlockScript block) && block._blockID == _blockGoal)
            {
                Debug.Log("Found a block with a matching goal", gameObject);
                float distance = Vector3.Distance(transform.position, gameObject.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    target = gameObject;
                }
            }
        }

        return target;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (TryGetComponent<BlockScript>(out BlockScript block) && block._blockID == _blockGoal)
        {
            Debug.Log("Found the block");
            Destroy(collision.gameObject);
        }
    }
}
