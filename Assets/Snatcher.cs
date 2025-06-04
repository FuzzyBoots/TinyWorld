using NUnit.Framework;
using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

[RequireComponent (typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class Snatcher : MonoBehaviour
{
    NavMeshAgent _agent;
    Animator _animator;
    [SerializeField] GameObject _target;
    [SerializeField] Vector3 _randomDestination;

    [SerializeField] private float _searchRadius;
    [SerializeField] private int _blockGoal;
    [SerializeField] private float _threshold = 2f;

    [SerializeField] GameObject blockPlacer;

    public PortalScript portal;

    public void SetGoal(int goal)
    {
        _blockGoal = goal;
    }

    public bool IsHolding { get; internal set; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();

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
        if (IsHolding)
        {
            _agent.SetDestination(portal.transform.position);
        } else if (_target != null)
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

        _animator.SetFloat("Speed", _agent.speed);
    }

    private GameObject LocateTarget()
    {
        GameObject target = null;
        if (blockPlacer != null)
        {
            BlockScript[] blocks = blockPlacer.GetComponentsInChildren<BlockScript>();

            if (blocks.Length > 0)            
            {
                int index = Random.Range(0, blocks.Length);
                if (blocks[index]._blockID == _blockGoal)
                {
                    target = blocks[index].gameObject;
                } else
                {
                    // Debug.Log($"Rejected block with ID of {blocks[index]._blockID}. Wanted {_blockGoal}");
                }
            }
        }        

        return target;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.TryGetComponent<BlockScript>(out BlockScript block) && block._blockID == _blockGoal)
        {
            _animator.SetBool("Holding", true);
            IsHolding = true;
            other.gameObject.transform.parent = transform;
            _agent.SetDestination(portal.gameObject.transform.position);
        }
    }
}
