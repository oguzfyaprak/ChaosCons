using FishNet.Object;
using UnityEngine;
using System.Collections;

namespace FishNet.Demo.Benchmarks.NetworkTransforms
{
    public class PrefabSpawner : NetworkBehaviour
    {
        [Header("General")]
        [SerializeField]
        private NetworkObject _prefab;

        [Header("Spawning")]
        [SerializeField]
        private int _maxSpawnCount = 6; // Maksimum 6 eşya
        [SerializeField]
        private float _spawnInterval = 5f;

        private int _currentSpawnCount = 0; // Şu anki eşya sayısını takip etmek için

        public override void OnStartServer()
        {
            if (_prefab == null)
            {
                Debug.LogError("Prefab is null.");
                return;
            }

            StartCoroutine(SpawnPrefabsCoroutine());
        }

        private IEnumerator SpawnPrefabsCoroutine()
        {
            Vector3 currentPosition = transform.position;

            while (_currentSpawnCount < _maxSpawnCount)
            {
                NetworkObject nob = Instantiate(_prefab, currentPosition, Quaternion.identity);
                base.Spawn(nob);
                _currentSpawnCount++;

                Debug.Log($"Spawned object {_currentSpawnCount}/{_maxSpawnCount}");

                // Son eşyayı spawn ettikten sonra döngüyü kır
                if (_currentSpawnCount >= _maxSpawnCount)
                    yield break;

                yield return new WaitForSeconds(_spawnInterval);
            }
        }

        // Eşya yok edildiğinde çağrılacak metod
        [Server]
        public void ObjectDestroyed()
        {
            _currentSpawnCount--;
            Debug.Log($"Object destroyed. Current count: {_currentSpawnCount}");

            // Eğer count azaldıysa yeniden spawn başlat
            if (_currentSpawnCount < _maxSpawnCount)
            {
                StartCoroutine(SpawnPrefabsCoroutine());
            }
        }
    }
}