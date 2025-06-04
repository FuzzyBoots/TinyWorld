using System.Collections;
using UnityEngine;

public class PortalScript : MonoBehaviour
{
    [SerializeField] float _spawnDelay = 2.0f;
    [SerializeField] float _initialDelay = 60f;
    [SerializeField] private Snatcher _alienPrefab;

    private void Start()
    {
        StartCoroutine(SpawnAliens());
    }

    private IEnumerator SpawnAliens()
    {
        yield return new WaitForSeconds(_initialDelay);
        while (true) {
            yield return new WaitForSeconds(_spawnDelay);
            Snatcher snatcher = Instantiate(_alienPrefab);
            snatcher.SetGoal(Random.Range(0, 6));
            snatcher.portal = this;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (TryGetComponent<Snatcher>(out Snatcher snatcher))
        {
            if (snatcher.IsHolding)
            {
                Destroy(snatcher.gameObject);
                // Change score?
            }
        }
    }
}

