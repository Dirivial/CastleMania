using JetBrains.Annotations;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class EnemyManager : MonoBehaviour
{
    public bool spawnEnemies = true;
    public Transform target;
    public List<Enemy> enemyTypes;
    public float minDistance = 80f;
    public float closestSpawnDistance = 20f;
    public float furthestSpawnDistance = 50f;
    public int wave = 1;
    public int maxWaves = 10;
    public float waveTimeout = 100f;
    public AnimationCurve enemiesPerWaveCurve = new AnimationCurve();
    public UIManager uiManager;
    public SceneSwitcher sceneSwitcher;

    public MMFeedbacks playerHitFeedback;

    private float timeToWave = 5f;
    private int numEnemies = 0;
    private int score = 0;
    private int playerHealth = 100;

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
        if (target.transform.position.y <= -40)
        {
            sceneSwitcher.SwitchToScene("Start Screen");
        }

        if (!spawnEnemies) return;

        timeToWave -= Time.deltaTime;
        uiManager.SetTimeToWaveText(Mathf.RoundToInt(timeToWave));

        if (timeToWave <= 0.0f)
        {
            wave++;
            timeToWave = waveTimeout;

            int numOfEnemies = Mathf.RoundToInt(enemiesPerWaveCurve.Evaluate(wave < maxWaves ? wave : maxWaves));
            numEnemies += numOfEnemies;
            uiManager.SetEnemiesLeftText(numEnemies);
            
            for (int i = 0; i < numOfEnemies; i++)
            {

                SpawnEnemyAt(UnityEngine.Random.Range(0, enemyTypes.Count), CreateRandomLocation());
            }
            Debug.Log("Wave # " + wave);
        }

        MoveEnemies();

        if (playerHealth <= 0)
        {
            sceneSwitcher.SwitchToScene("Start Screen");
        }
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
        List<Enemy> enemiesToRemove = new List<Enemy>();
        foreach (Enemy e in aliveEnemies)
        {
            float distance = Vector3.Distance(e.transform.position, target.position);
            if (Mathf.Abs(distance) > minDistance)
            {
                Vector3 direction = Vector3.MoveTowards(e.transform.position, new Vector3(target.position.x, target.position.y + 1f, target.position.z), e.speed * Time.deltaTime);

                e.transform.position = direction;
            }
            else
            {
                enemiesToRemove.Add(e);
            }
        }
        foreach (Enemy e in enemiesToRemove)
        {
            score -= 5;
            playerHealth -= 10;
            uiManager.SetScoreText(score);
            uiManager.SetHealthText(playerHealth);
            playerHitFeedback.PlayFeedbacks();
            e.Remove(false);
        }
    }

    private void SpawnEnemy(int id)
    {
        aliveEnemies.Add(enemyPools[id].Get());
    }

    private void SpawnEnemyAt(int id, Vector3 position)
    {
        Enemy e = enemyPools[id].Get();
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

    public void DespawnEnemy(int id, Enemy enemy, bool gotKilled)
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
        numEnemies--;
        uiManager.SetEnemiesLeftText(numEnemies);

        if (gotKilled)
        {
            score += 10 + wave * 2;
            uiManager.SetScoreText(score);
        }
    }
}
