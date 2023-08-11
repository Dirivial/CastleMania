using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class EnemyManager : MonoBehaviour
{
    public Transform target;
    public List<Enemy> enemyTypes;
    public float closestSpawnDistance = 20f;
    public float furthestSpawnDistance = 50f;
    public int wave = 1;
    public float waveTimeout = 100f;

    private float timeToWave = 5f;

    private List<ObjectPool<Enemy>> enemyPools = new List<ObjectPool<Enemy>>();
    private List<Enemy> aliveEnemies;

    private void Start()
    {
        aliveEnemies = new List<Enemy>();
        CreateEnemyPools();
    }

    // Update is called once per frame
    void Update()
    {
        timeToWave -= Time.deltaTime;

        if (timeToWave <= 0.0f)
        {
            wave++;
            timeToWave = waveTimeout;

            for (int i = 0; i < wave * wave; i++)
            {

                SpawnEnemyAt(UnityEngine.Random.Range(0, enemyTypes.Count), CreateRandomLocation());
            }
            Debug.Log("Wave # " + wave);
        }

        MoveEnemies();
    }

    private Vector3 CreateRandomLocation()
    {
        float deltaX = UnityEngine.Random.Range(closestSpawnDistance, furthestSpawnDistance) * (UnityEngine.Random.Range(0, 2) == 0 ? -1 : 1);
        float deltaY = UnityEngine.Random.Range(closestSpawnDistance, furthestSpawnDistance) * (UnityEngine.Random.Range(0, 2) == 0 ? -1 : 1);
        float deltaZ = UnityEngine.Random.Range(closestSpawnDistance, furthestSpawnDistance) * (UnityEngine.Random.Range(0, 2) == 0 ? -1 : 1);
        return new Vector3(target.position.x + deltaX, target.position.y + deltaY, target.position.z + deltaZ);
    }

    private void MoveEnemies()
    {
        foreach (Enemy e in aliveEnemies)
        {
            Vector3 direction = Vector3.MoveTowards(e.transform.position, new Vector3(target.position.x, target.position.y + 1f, target.position.z), e.speed * Time.deltaTime);
            if (direction.magnitude > 0.01f)
            {
                e.transform.position = direction;
            }
        }
    }

    private void SpawnEnemy(int id)
    {
        aliveEnemies.Add(enemyPools[id].Get());
    }

    private void SpawnEnemyAt(int id, Vector3 position)
    {
        Enemy e = enemyPools[id].Get();
        Debug.Log("Spawning type " + id);
        e.transform.position = position;
        aliveEnemies.Add(e);
    }

    private void CreateEnemyPools()
    {
        int id = 0;
        foreach (Enemy enemy in enemyTypes)
        {
            enemy.SetID(id);
            ObjectPool<Enemy> enemyPool = new ObjectPool<Enemy>(() =>
            {
                Enemy e = Instantiate(enemy, transform);
                e.Create(DespawnEnemy);
                return e;
            }, obj => {
                obj.Spawn();
                obj.gameObject.SetActive(true);
            }, obj =>
            {
                obj.gameObject.SetActive(false);
            }, obj =>
            {
                Destroy(obj);
            }, false, 10, 100);
            enemyPools.Add(enemyPool);
            id++;
        }
    }

    public void DespawnEnemy(int id, Enemy enemy)
    {
        foreach (Enemy e in aliveEnemies)
        {
            if (e.Equals(enemy))
            {
                aliveEnemies.Remove(e);
                break;
            }
        }
        enemyPools[id].Release(enemy);
    }
}
