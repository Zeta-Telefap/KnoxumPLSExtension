// Проклятье Мудрого дерева

using System;

// Token: 0x02000001 RID: 1
internal class <Module>
{
}

using System;
using UnityEngine;

// Token: 0x02000006 RID: 6
public class AfterTitles : MonoBehaviour
{
    // Token: 0x0600001E RID: 30 RVA: 0x00002A9C File Offset: 0x00000C9C
    public void OnTitlesOver()
    {
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        PlayerPrefs.DeleteAll();
        Cursor.lockState = CursorLockMode.None;
        this.menu.OnExit();
    }

    // Token: 0x0400001F RID: 31
    public MenuExit menu;
}

using System;
using UnityEngine;

// Token: 0x02000007 RID: 7
public class BasicEnemy : MonoBehaviour
{
    // Token: 0x06000020 RID: 32 RVA: 0x00002AC7 File Offset: 0x00000CC7
    public void TakeDamage(int damage)
    {
        this.health -= damage;
        if (this.health <= 0)
        {
            this.DestroyEnemy();
        }
    }

    // Token: 0x06000021 RID: 33 RVA: 0x00002AE6 File Offset: 0x00000CE6
    public void DestroyEnemy()
    {
        Object.Destroy(base.gameObject);
    }

    // Token: 0x04000020 RID: 32
    [Header("Stats")]
    public int health;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Token: 0x02000008 RID: 8
public class Boss : MonoBehaviour
{
    // Token: 0x06000023 RID: 35 RVA: 0x00002AFC File Offset: 0x00000CFC
    private void Awake()
    {
        this.player = GameObject.FindGameObjectWithTag("Player").transform;
        this.agent = base.GetComponent<NavMeshAgent>();
        this.health = this.startHealth;
        this.healthSlider = Object.FindObjectOfType<MushroomGame>().bossHP.GetComponent<Slider>();
    }

    // Token: 0x06000024 RID: 36 RVA: 0x00002B4B File Offset: 0x00000D4B
    private void Start()
    {
        this.healthSlider.gameObject.SetActive(true);
    }

    // Token: 0x06000025 RID: 37 RVA: 0x00002B60 File Offset: 0x00000D60
    private void Update()
    {
        this.healthSlider.value = this.health / this.startHealth;
        this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
        if (!this.playerInSightRange && !this.playerInAttackRange)
        {
            this.Patroling();
        }
        if (this.playerInSightRange && !this.playerInAttackRange)
        {
            this.ChasePlayer();
        }
        if (this.playerInAttackRange && this.playerInSightRange)
        {
            this.AttackPlayer();
        }
    }

    // Token: 0x06000026 RID: 38 RVA: 0x00002C18 File Offset: 0x00000E18
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x06000027 RID: 39 RVA: 0x00002C7C File Offset: 0x00000E7C
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x06000028 RID: 40 RVA: 0x00002D22 File Offset: 0x00000F22
    private void ChasePlayer()
    {
        this.agent.SetDestination(this.player.position);
        if (!this.isAttacking)
        {
            this.isAttacking = true;
            this.ChooseMoveset();
        }
    }

    // Token: 0x06000029 RID: 41 RVA: 0x00002D50 File Offset: 0x00000F50
    private void AttackPlayer()
    {
        this.agent.SetDestination(base.transform.position);
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x0600002A RID: 42 RVA: 0x00002DEC File Offset: 0x00000FEC
    public void ChooseMoveset()
    {
        int num = Random.Range(0, 3);
        if (num == 0)
        {
            base.StartCoroutine(this.SpawnGroupSticks(false, 5, 0.3f));
            return;
        }
        if (num != 1)
        {
            if (num == 2)
            {
                for (int i = 0; i < 7; i++)
                {
                    base.StartCoroutine(this.SummonLightning());
                }
            }
            return;
        }
        if (this.health > 15f)
        {
            base.StartCoroutine(this.SpawnGroupSticks(true, 3, 0.6f));
            return;
        }
        base.StartCoroutine(this.InstantiateEarthquake());
    }

    // Token: 0x0600002B RID: 43 RVA: 0x00002E6A File Offset: 0x0000106A
    public IEnumerator SpawnGroupSticks(bool isNormal, int totalSticks, float cooldown)
    {
        int numberOfSticks = 0;
        while (numberOfSticks <= totalSticks)
        {
            this.SpawnStick(isNormal);
            int num = numberOfSticks;
            numberOfSticks = num + 1;
            yield return new WaitForSeconds(cooldown);
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x0600002C RID: 44 RVA: 0x00002E90 File Offset: 0x00001090
    public void SpawnStick(bool isNormal)
    {
        if (isNormal)
        {
            Object.Instantiate<GameObject>(this.stick, this.attackPosNormal.position, Quaternion.Euler(0f, (float)Random.Range(0, 360), (float)Random.Range(0, 360)));
        }
        else
        {
            Object.Instantiate<GameObject>(this.stick, this.attackPosDown.position, this.attackPosDown.rotation);
        }
        Object.Instantiate<GameObject>(this.magicSound, this.attackPosNormal.position, Quaternion.identity);
    }

    // Token: 0x0600002D RID: 45 RVA: 0x00002F19 File Offset: 0x00001119
    public IEnumerator SummonLightning()
    {
        Vector3 lightningPos = this.RandomPointInBox(new Vector3(15f, 0f, 15f));
        Object.Instantiate<GameObject>(this.preLightning, lightningPos, Quaternion.identity);
        yield return new WaitForSeconds(1f);
        Object.Instantiate<GameObject>(this.lightning, lightningPos, Quaternion.identity);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPosNormal.position, Quaternion.identity);
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x0600002E RID: 46 RVA: 0x00002F28 File Offset: 0x00001128
    public IEnumerator InstantiateEarthquake()
    {
        Object.Instantiate<GameObject>(this.earthquake, this.lightningCenter.position, this.lightningCenter.rotation);
        yield return new WaitForSeconds(3f);
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x0600002F RID: 47 RVA: 0x00002F38 File Offset: 0x00001138
    private Vector3 RandomPointInBox(Vector3 size)
    {
        return new Vector3(this.player.position.x, 0f, this.player.position.z) + new Vector3((Random.value - 0.5f) * size.x, (Random.value - 0.5f) * size.y, (Random.value - 0.5f) * size.z);
    }

    // Token: 0x06000030 RID: 48 RVA: 0x00002FAF File Offset: 0x000011AF
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x06000031 RID: 49 RVA: 0x00002FB8 File Offset: 0x000011B8
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x06000032 RID: 50 RVA: 0x00002FE8 File Offset: 0x000011E8
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.finalMushroom, base.transform.position, Quaternion.identity);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x06000033 RID: 51 RVA: 0x00003044 File Offset: 0x00001244
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x04000021 RID: 33
    public LayerMask whatIsGround;

    // Token: 0x04000022 RID: 34
    public LayerMask whatIsPlayer;

    // Token: 0x04000023 RID: 35
    public Vector3 walkPoint;

    // Token: 0x04000024 RID: 36
    private bool walkPointSet;

    // Token: 0x04000025 RID: 37
    public float walkPointRange;

    // Token: 0x04000026 RID: 38
    public float timeBetweenAttacks;

    // Token: 0x04000027 RID: 39
    private bool isAttacking;

    // Token: 0x04000028 RID: 40
    private bool alreadyAttacked;

    // Token: 0x04000029 RID: 41
    public float sightRange;

    // Token: 0x0400002A RID: 42
    public float attackRange;

    // Token: 0x0400002B RID: 43
    public bool playerInSightRange;

    // Token: 0x0400002C RID: 44
    public bool playerInAttackRange;

    // Token: 0x0400002D RID: 45
    public float startHealth;

    // Token: 0x0400002E RID: 46
    private float health;

    // Token: 0x0400002F RID: 47
    public int damage;

    // Token: 0x04000030 RID: 48
    public GameObject stick;

    // Token: 0x04000031 RID: 49
    public GameObject preLightning;

    // Token: 0x04000032 RID: 50
    public GameObject lightning;

    // Token: 0x04000033 RID: 51
    public GameObject earthquake;

    // Token: 0x04000034 RID: 52
    public Transform attackPosNormal;

    // Token: 0x04000035 RID: 53
    public Transform attackPosDown;

    // Token: 0x04000036 RID: 54
    public Transform lightningCenter;

    // Token: 0x04000037 RID: 55
    public GameObject magicSound;

    // Token: 0x04000038 RID: 56
    public GameObject deathEffect;

    // Token: 0x04000039 RID: 57
    public GameObject finalMushroom;

    // Token: 0x0400003A RID: 58
    public Slider healthSlider;

    // Token: 0x0400003B RID: 59
    private NavMeshAgent agent;

    // Token: 0x0400003C RID: 60
    private Transform player;

    // Token: 0x0400003D RID: 61
    private GameObject newStick;

    // Token: 0x0400003E RID: 62
    private Vector3 stickDirection;
}

using System;
using UnityEngine;

// Token: 0x02000009 RID: 9
public class BossMovesets : MonoBehaviour
{
    // Token: 0x06000035 RID: 53 RVA: 0x00003099 File Offset: 0x00001299
    private void Start()
    {
    }

    // Token: 0x06000036 RID: 54 RVA: 0x0000309B File Offset: 0x0000129B
    private void Update()
    {
    }
}

using System;
using UnityEngine;

// Token: 0x0200000A RID: 10
public class CaveSoundDetector : MonoBehaviour
{
    // Token: 0x06000038 RID: 56 RVA: 0x000030A5 File Offset: 0x000012A5
    private void Start()
    {
        this.music = Object.FindObjectOfType<MusicManager>();
    }

    // Token: 0x06000039 RID: 57 RVA: 0x000030B2 File Offset: 0x000012B2
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (!this.music.musicVariants[4].isPlaying)
            {
                this.music.EnableMusic(4);
                return;
            }
            this.music.EnableMusic(0);
        }
    }

    // Token: 0x0400003F RID: 63
    private MusicManager music;
}

using System;
using UnityEngine;

// Token: 0x0200000B RID: 11
public class DamageDealer : MonoBehaviour
{
    // Token: 0x0600003B RID: 59 RVA: 0x000030F6 File Offset: 0x000012F6
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.GetComponent<PlayerHealth>() != null)
        {
            other.transform.root.GetComponent<PlayerHealth>().TakeDamage(this.damage);
        }
    }

    // Token: 0x04000040 RID: 64
    public float damage;
}

using System;
using UnityEngine;

// Token: 0x0200000C RID: 12
public class Destroyer : MonoBehaviour
{
    // Token: 0x0600003D RID: 61 RVA: 0x00003133 File Offset: 0x00001333
    private void Start()
    {
        Object.Destroy(base.gameObject, this.lifetime);
    }

    // Token: 0x04000041 RID: 65
    public float lifetime;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x0200000D RID: 13
public class DoorInteraction : MonoBehaviour
{
    // Token: 0x0600003F RID: 63 RVA: 0x0000314E File Offset: 0x0000134E
    private void Update()
    {
        this.playerNearDoor = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        if (this.playerNearDoor && Input.GetKeyDown(KeyCode.E))
        {
            this.OpenDoor();
        }
    }

    // Token: 0x06000040 RID: 64 RVA: 0x00003190 File Offset: 0x00001390
    public void OpenDoor()
    {
        this.doorAnimator.SetBool("isOpened", true);
        this.gateAnimator.SetBool("hasMushrooms", true);
        this.enemyRun.speed = 8f;
        this.enemyRun.GetComponent<AudioSource>().Play();
        Object.FindObjectOfType<MusicManager>().EnableMusic(3);
        base.enabled = false;
    }

    // Token: 0x06000041 RID: 65 RVA: 0x000031F1 File Offset: 0x000013F1
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x04000042 RID: 66
    public Animator doorAnimator;

    // Token: 0x04000043 RID: 67
    public Animator gateAnimator;

    // Token: 0x04000044 RID: 68
    public NavMeshAgent enemyRun;

    // Token: 0x04000045 RID: 69
    public LayerMask whatIsPlayer;

    // Token: 0x04000046 RID: 70
    public float sightRange;

    // Token: 0x04000047 RID: 71
    private bool playerNearDoor;
}

using System;
using UnityEngine;

// Token: 0x0200000E RID: 14
public class EndGame : MonoBehaviour
{
    // Token: 0x06000043 RID: 67 RVA: 0x0000321C File Offset: 0x0000141C
    public void FadeToTitles()
    {
        this.magicPickSound.Play();
        Object.FindObjectOfType<MusicManager>().EnableMusic(2);
        base.gameObject.SetActive(false);
        AfterTitles[] array = Object.FindObjectsOfType<AfterTitles>(true);
        for (int i = 0; i < array.Length; i++)
        {
            array[i].gameObject.SetActive(true);
        }
    }

    // Token: 0x04000048 RID: 72
    public AudioSource magicPickSound;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x0200000F RID: 15
public class Enemy : MonoBehaviour
{
    // Token: 0x06000045 RID: 69 RVA: 0x00003276 File Offset: 0x00001476
    private void Awake()
    {
        this.player = GameObject.FindGameObjectWithTag("Player").transform;
        this.agent = base.GetComponent<NavMeshAgent>();
    }

    // Token: 0x06000046 RID: 70 RVA: 0x0000329C File Offset: 0x0000149C
    private void Update()
    {
        this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
        if (!this.playerInSightRange && !this.playerInAttackRange)
        {
            this.Patroling();
        }
        if (this.playerInSightRange && !this.playerInAttackRange)
        {
            this.ChasePlayer();
        }
        if (this.playerInAttackRange && this.playerInSightRange)
        {
            this.AttackPlayer();
        }
    }

    // Token: 0x06000047 RID: 71 RVA: 0x0000333C File Offset: 0x0000153C
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x06000048 RID: 72 RVA: 0x000033A0 File Offset: 0x000015A0
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x06000049 RID: 73 RVA: 0x00003446 File Offset: 0x00001646
    private void ChasePlayer()
    {
        this.agent.SetDestination(this.player.position);
    }

    // Token: 0x0600004A RID: 74 RVA: 0x00003460 File Offset: 0x00001660
    private void AttackPlayer()
    {
        this.agent.SetDestination(base.transform.position);
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x0600004B RID: 75 RVA: 0x000034FB File Offset: 0x000016FB
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x0600004C RID: 76 RVA: 0x00003504 File Offset: 0x00001704
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x0600004D RID: 77 RVA: 0x00003532 File Offset: 0x00001732
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x0600004E RID: 78 RVA: 0x0000355C File Offset: 0x0000175C
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x04000049 RID: 73
    public NavMeshAgent agent;

    // Token: 0x0400004A RID: 74
    public Transform player;

    // Token: 0x0400004B RID: 75
    public LayerMask whatIsGround;

    // Token: 0x0400004C RID: 76
    public LayerMask whatIsPlayer;

    // Token: 0x0400004D RID: 77
    public Vector3 walkPoint;

    // Token: 0x0400004E RID: 78
    private bool walkPointSet;

    // Token: 0x0400004F RID: 79
    public float walkPointRange;

    // Token: 0x04000050 RID: 80
    public float timeBetweenAttacks;

    // Token: 0x04000051 RID: 81
    private bool alreadyAttacked;

    // Token: 0x04000052 RID: 82
    public float sightRange;

    // Token: 0x04000053 RID: 83
    public float attackRange;

    // Token: 0x04000054 RID: 84
    public bool playerInSightRange;

    // Token: 0x04000055 RID: 85
    public bool playerInAttackRange;

    // Token: 0x04000056 RID: 86
    public float health;

    // Token: 0x04000057 RID: 87
    public int damage;

    // Token: 0x04000058 RID: 88
    public GameObject deathEffect;
}

using System;
using UnityEngine;

// Token: 0x02000010 RID: 16
public class EnemySpawner : MonoBehaviour
{
    // Token: 0x06000050 RID: 80 RVA: 0x000035B4 File Offset: 0x000017B4
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            int num = Random.Range(0, this.spawnPoints.Length);
            int num2 = Random.Range(0, this.enemies.Length);
            Object.Instantiate<GameObject>(this.enemies[num2], this.spawnPoints[num].position, Quaternion.identity);
            base.enabled = false;
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x04000059 RID: 89
    public Transform[] spawnPoints;

    // Token: 0x0400005A RID: 90
    public GameObject[] enemies;
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

// Token: 0x02000011 RID: 17
public class GameSettings : MonoBehaviour
{
    // Token: 0x06000052 RID: 82 RVA: 0x00003626 File Offset: 0x00001826
    private void Start()
    {
        this.LoadValues();
    }

    // Token: 0x06000053 RID: 83 RVA: 0x0000362E File Offset: 0x0000182E
    public void TextToSlider(float sensitivity)
    {
        this.sliderPercentUI.text = string.Format("чувствительность мыши: {0}%", Mathf.RoundToInt(sensitivity / 16f));
    }

    // Token: 0x06000054 RID: 84 RVA: 0x00003656 File Offset: 0x00001856
    public void TickState()
    {
        if (this.postEffectsEnabled == 0)
        {
            this.tick.SetActive(false);
            this.postProcess.enabled = false;
            return;
        }
        this.tick.SetActive(true);
        this.postProcess.enabled = true;
    }

    // Token: 0x06000055 RID: 85 RVA: 0x00003694 File Offset: 0x00001894
    public void SaveSensitivity()
    {
        float value = this.sensitivitySlider.value;
        PlayerPrefs.SetFloat("SensitivityValue", value);
        this.LoadValues();
    }

    // Token: 0x06000056 RID: 86 RVA: 0x000036C0 File Offset: 0x000018C0
    public void SaveEffects()
    {
        if (this.tick.activeSelf)
        {
            this.tick.SetActive(false);
            this.postEffectsEnabled = 0;
        }
        else
        {
            this.tick.SetActive(true);
            this.postEffectsEnabled = 1;
        }
        PlayerPrefs.SetInt("PostEffects", this.postEffectsEnabled);
        this.LoadValues();
    }

    // Token: 0x06000057 RID: 87 RVA: 0x00003718 File Offset: 0x00001918
    public void SaveQuality(int index)
    {
        this.graphicsQuality = index;
        PlayerPrefs.SetInt("GraphicsQuality", this.graphicsQuality);
    }

    // Token: 0x06000058 RID: 88 RVA: 0x00003734 File Offset: 0x00001934
    public void LoadValues()
    {
        float num = 400f;
        int num2 = 1;
        int qualityLevel = 2;
        if (PlayerPrefs.HasKey("SensitivityValue"))
        {
            num = PlayerPrefs.GetFloat("SensitivityValue");
        }
        if (PlayerPrefs.HasKey("PostEffects"))
        {
            num2 = PlayerPrefs.GetInt("PostEffects");
        }
        if (PlayerPrefs.HasKey("GraphicsQuality"))
        {
            qualityLevel = PlayerPrefs.GetInt("GraphicsQuality");
        }
        this.sensitivitySlider.value = num;
        this.postEffectsEnabled = num2;
        this.graphicsQuality = qualityLevel;
        this.qualityDropdown.SetQualityLevel(qualityLevel);
        this.TextToSlider(num);
        this.TickState();
        this.playerCamera.sensitivityX = (this.playerCamera.sensitivityY = num);
    }

    // Token: 0x0400005B RID: 91
    [SerializeField]
    private Slider sensitivitySlider;

    // Token: 0x0400005C RID: 92
    [SerializeField]
    private PostProcessLayer postProcess;

    // Token: 0x0400005D RID: 93
    [SerializeField]
    private PlayerCamera playerCamera;

    // Token: 0x0400005E RID: 94
    [SerializeField]
    private TextMeshProUGUI sliderPercentUI;

    // Token: 0x0400005F RID: 95
    [SerializeField]
    private SetQuality qualityDropdown;

    // Token: 0x04000060 RID: 96
    public int postEffectsEnabled;

    // Token: 0x04000061 RID: 97
    public int graphicsQuality;

    // Token: 0x04000062 RID: 98
    [SerializeField]
    private GameObject tick;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x02000012 RID: 18
public class GribmanActivator : MonoBehaviour
{
    // Token: 0x0600005A RID: 90 RVA: 0x000037E5 File Offset: 0x000019E5
    private void Start()
    {
        Object.Instantiate<GameObject>(this.gribmanSound, base.transform.position, Quaternion.identity);
        base.Invoke("AttackPlayer", 8.5f);
    }

    // Token: 0x0600005B RID: 91 RVA: 0x00003813 File Offset: 0x00001A13
    public void AttackPlayer()
    {
        base.GetComponent<NavMeshAgent>().speed = 10f;
        Object.Instantiate<GameObject>(this.attackSound, base.transform.position, Quaternion.identity);
        base.Invoke("AttackPlayer", 8.5f);
    }

    // Token: 0x04000063 RID: 99
    public GameObject gribmanSound;

    // Token: 0x04000064 RID: 100
    public GameObject attackSound;
}

using System;
using UnityEngine;

// Token: 0x02000013 RID: 19
public class Item : MonoBehaviour
{
    // Token: 0x0600005D RID: 93 RVA: 0x00003859 File Offset: 0x00001A59
    private void Start()
    {
    }

    // Token: 0x0600005E RID: 94 RVA: 0x0000385B File Offset: 0x00001A5B
    private void Update()
    {
    }
}

using System;
using UnityEngine;

// Token: 0x02000014 RID: 20
public class LightSwitcher : MonoBehaviour
{
    // Token: 0x06000060 RID: 96 RVA: 0x00003865 File Offset: 0x00001A65
    private void Update()
    {
        if (Input.GetAxis("Mouse ScrollWheel") != 0f || Input.GetKeyDown(KeyCode.Q))
        {
            this.SwitchLight();
        }
    }

    // Token: 0x06000061 RID: 97 RVA: 0x00003888 File Offset: 0x00001A88
    private void SwitchLight()
    {
        if (this.oilLamp.activeInHierarchy)
        {
            this.oilLamp.SetActive(false);
            this.flashlight.SetActive(true);
            return;
        }
        if (this.flashlight.activeInHierarchy)
        {
            this.flashlight.SetActive(false);
            this.oilLamp.SetActive(true);
        }
    }

    // Token: 0x04000065 RID: 101
    public GameObject flashlight;

    // Token: 0x04000066 RID: 102
    public GameObject oilLamp;
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Token: 0x02000015 RID: 21
public class Menu : MonoBehaviour
{
    // Token: 0x06000063 RID: 99 RVA: 0x000038E8 File Offset: 0x00001AE8
    public void LoadGame()
    {
        SceneManager.LoadScene("Game");
    }

    // Token: 0x06000064 RID: 100 RVA: 0x000038F4 File Offset: 0x00001AF4
    public void LoadNewGame()
    {
        PlayerPrefs.DeleteAll();
        this.LoadGame();
    }
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Token: 0x02000016 RID: 22
public class MenuExit : MonoBehaviour
{
    // Token: 0x06000066 RID: 102 RVA: 0x00003909 File Offset: 0x00001B09
    public void OnExit()
    {
        Time.timeScale = 1f;
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        this.SaveMushroomsPresence();
        SceneManager.LoadScene("Menu");
    }

    // Token: 0x06000067 RID: 103 RVA: 0x00003930 File Offset: 0x00001B30
    public void SaveMushroomsPresence()
    {
        Mushroom[] array = this.mushrooms;
        for (int i = 0; i < array.Length; i++)
        {
            array[i].SavePresence();
        }
    }

    // Token: 0x04000067 RID: 103
    public Mushroom[] mushrooms;
}

using System;
using UnityEngine;

// Token: 0x02000003 RID: 3
public class MouseLook : MonoBehaviour
{
    // Token: 0x06000004 RID: 4 RVA: 0x0000214D File Offset: 0x0000034D
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Token: 0x06000005 RID: 5 RVA: 0x00002158 File Offset: 0x00000358
    private void Update()
    {
        float d = Input.GetAxis("Mouse X") * this.mouseSensitivity * Time.deltaTime;
        float num = Input.GetAxis("Mouse Y") * this.mouseSensitivity * Time.deltaTime;
        this.xRotation -= num;
        this.xRotation = Mathf.Clamp(this.xRotation, -90f, 90f);
        base.transform.localRotation = Quaternion.Euler(this.xRotation, 0f, 0f);
        this.playerBody.Rotate(Vector3.up * d);
    }

    // Token: 0x04000008 RID: 8
    public float mouseSensitivity = 100f;

    // Token: 0x04000009 RID: 9
    public Transform playerBody;

    // Token: 0x0400000A RID: 10
    private float xRotation;
}

using System;
using UnityEngine;

// Token: 0x02000017 RID: 23
public class MoveCamera : MonoBehaviour
{
    // Token: 0x06000069 RID: 105 RVA: 0x00003962 File Offset: 0x00001B62
    private void Update()
    {
        base.transform.position = this.cameraPosition.position;
    }

    // Token: 0x04000068 RID: 104
    public Transform cameraPosition;
}

using System;
using UnityEngine;

// Token: 0x02000018 RID: 24
public class MoveForward : MonoBehaviour
{
    // Token: 0x0600006B RID: 107 RVA: 0x00003982 File Offset: 0x00001B82
    private void Update()
    {
        base.transform.Translate(Vector3.forward * this.speed * Time.deltaTime);
    }

    // Token: 0x04000069 RID: 105
    public float speed;
}

using System;
using UnityEngine;

// Token: 0x02000019 RID: 25
public class Mushroom : MonoBehaviour
{
    // Token: 0x0600006D RID: 109 RVA: 0x000039B1 File Offset: 0x00001BB1
    private void Start()
    {
        this.LoadPresence();
    }

    // Token: 0x0600006E RID: 110 RVA: 0x000039B9 File Offset: 0x00001BB9
    public void OnCollect()
    {
        this.isCollected = "yes";
        base.gameObject.SetActive(false);
    }

    // Token: 0x0600006F RID: 111 RVA: 0x000039D2 File Offset: 0x00001BD2
    public void SavePresence()
    {
        PlayerPrefs.SetString("isCollected" + base.gameObject.name, this.isCollected);
    }

    // Token: 0x06000070 RID: 112 RVA: 0x000039F4 File Offset: 0x00001BF4
    public void LoadPresence()
    {
        this.isCollected = PlayerPrefs.GetString("isCollected" + base.gameObject.name);
        this.CheckPresence();
    }

    // Token: 0x06000071 RID: 113 RVA: 0x00003A1C File Offset: 0x00001C1C
    public void CheckPresence()
    {
        if (this.isCollected == "yes")
        {
            base.gameObject.SetActive(false);
        }
    }

    // Token: 0x0400006A RID: 106
    public string isCollected;
}

using System;
using UnityEngine;

// Token: 0x0200001A RID: 26
public class MushroomChecker : MonoBehaviour
{
    // Token: 0x06000073 RID: 115 RVA: 0x00003A44 File Offset: 0x00001C44
    private void Start()
    {
        this.collector = Object.FindObjectOfType<MushroomCollector>();
    }

    // Token: 0x06000074 RID: 116 RVA: 0x00003A54 File Offset: 0x00001C54
    private void Update()
    {
        this.playerNearGate = Physics.CheckSphere(this.openingPoint.position, this.sightRange, this.whatIsPlayer);
        this.playerPastGate = Physics.CheckSphere(this.closingPoint.position, this.sightRange, this.whatIsPlayer);
        if (this.playerNearGate)
        {
            this.CheckForMushrooms();
        }
        if (this.playerPastGate)
        {
            this.CloseGate();
        }
    }

    // Token: 0x06000075 RID: 117 RVA: 0x00003ACC File Offset: 0x00001CCC
    public void CheckForMushrooms()
    {
        if (this.collector.mushrooms >= this.mushroomsRequired)
        {
            this.gateAnimator.SetBool("hasMushrooms", true);
        }
        if (!this.alreadySaved)
        {
            this.alreadySaved = true;
            this.collector.SaveMushrooms();
            this.collector.GetComponent<PlayerController>().SavePosition();
            Object.FindObjectOfType<MenuExit>().SaveMushroomsPresence();
        }
    }

    // Token: 0x06000076 RID: 118 RVA: 0x00003B31 File Offset: 0x00001D31
    public void CloseGate()
    {
        this.gateAnimator.SetBool("pastGate", true);
        base.enabled = false;
    }

    // Token: 0x06000077 RID: 119 RVA: 0x00003B4C File Offset: 0x00001D4C
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(this.openingPoint.position, this.sightRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(this.closingPoint.position, this.sightRange);
    }

    // Token: 0x0400006B RID: 107
    public Transform openingPoint;

    // Token: 0x0400006C RID: 108
    public Transform closingPoint;

    // Token: 0x0400006D RID: 109
    public Animator gateAnimator;

    // Token: 0x0400006E RID: 110
    public LayerMask whatIsPlayer;

    // Token: 0x0400006F RID: 111
    public float sightRange;

    // Token: 0x04000070 RID: 112
    public int mushroomsRequired;

    // Token: 0x04000071 RID: 113
    private MushroomCollector collector;

    // Token: 0x04000072 RID: 114
    private bool playerNearGate;

    // Token: 0x04000073 RID: 115
    private bool playerPastGate;

    // Token: 0x04000074 RID: 116
    private bool alreadySaved;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x0200001B RID: 27
public class MushroomCollector : MonoBehaviour
{
    // Token: 0x06000079 RID: 121 RVA: 0x00003BA1 File Offset: 0x00001DA1
    private void Start()
    {
        this.LoadMushrooms();
    }

    // Token: 0x0600007A RID: 122 RVA: 0x00003BAC File Offset: 0x00001DAC
    private void Update()
    {
        RaycastHit raycastHit;
        if (Input.GetKeyDown(KeyCode.E) && Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out raycastHit, this.pickupRange, this.itemMask))
        {
            if (raycastHit.collider.GetComponent<Mushroom>() != null)
            {
                this.mushrooms++;
                this.mushroomText.text = (this.mushrooms.ToString() ?? "");
                raycastHit.collider.GetComponent<Mushroom>().OnCollect();
                return;
            }
            if (raycastHit.collider.CompareTag("Flower"))
            {
                base.GetComponent<PlayerController>().moveSpeed = 8f;
                Object.Destroy(raycastHit.collider.gameObject);
                return;
            }
            if (raycastHit.collider.CompareTag("StrangeMushroom"))
            {
                this.gribman.SetActive(true);
                return;
            }
            if (raycastHit.collider.GetComponent<EndGame>() != null)
            {
                raycastHit.collider.GetComponent<EndGame>().FadeToTitles();
            }
        }
    }

    // Token: 0x0600007B RID: 123 RVA: 0x00003CBE File Offset: 0x00001EBE
    public void SaveMushrooms()
    {
        PlayerPrefs.SetInt("mushrooms", this.mushrooms);
    }

    // Token: 0x0600007C RID: 124 RVA: 0x00003CD0 File Offset: 0x00001ED0
    public void LoadMushrooms()
    {
        this.mushrooms = PlayerPrefs.GetInt("mushrooms");
        this.mushroomText.text = (this.mushrooms.ToString() ?? "");
    }

    // Token: 0x04000075 RID: 117
    public TextMeshProUGUI mushroomText;

    // Token: 0x04000076 RID: 118
    public LayerMask itemMask;

    // Token: 0x04000077 RID: 119
    public int mushrooms;

    // Token: 0x04000078 RID: 120
    public float pickupRange;

    // Token: 0x04000079 RID: 121
    public GameObject gribman;
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Token: 0x0200001C RID: 28
public class MushroomGame : MonoBehaviour
{
    // Token: 0x0600007E RID: 126 RVA: 0x00003D09 File Offset: 0x00001F09
    private void OnTriggerEnter(Collider other)
    {
        if (!this.isReady && other.transform.root.CompareTag("Player"))
        {
            this.StartGame();
        }
    }

    // Token: 0x0600007F RID: 127 RVA: 0x00003D30 File Offset: 0x00001F30
    private void StartGame()
    {
        this.gameTime = this.startGameTime;
        this.timerText.gameObject.SetActive(true);
        this.isReady = true;
        base.GetComponent<SphereCollider>().enabled = false;
        Object.FindObjectOfType<MusicManager>().EnableMusic(1);
    }

    // Token: 0x06000080 RID: 128 RVA: 0x00003D70 File Offset: 0x00001F70
    private void Update()
    {
        if (this.isReady)
        {
            if (this.gameTime > 0f)
            {
                float num = (float)Mathf.FloorToInt(this.gameTime / 60f);
                float num2 = (float)Mathf.FloorToInt(this.gameTime % 60f);
                this.timerText.text = string.Format("{0:00}:{1:00}", num, num2);
                this.gameTime -= Time.deltaTime;
                if (this.timeBetweenSpawn > 0f)
                {
                    this.timeBetweenSpawn -= Time.deltaTime;
                    return;
                }
                int num3 = Random.Range(0, this.enemies.Length);
                int num4 = Random.Range(0, this.spawnPoints.Length);
                Object.Instantiate<GameObject>(this.enemies[num3], this.spawnPoints[num4].position, Quaternion.identity);
                this.timeBetweenSpawn = this.startTimeBetweenSpawn;
                if (this.startTimeBetweenSpawn > this.minTime)
                {
                    this.startTimeBetweenSpawn -= this.decreaseTime;
                    return;
                }
            }
            else if (!this.alreadyStarted)
            {
                this.alreadyStarted = true;
                base.StartCoroutine(this.StartBossfight());
            }
        }
    }

    // Token: 0x06000081 RID: 129 RVA: 0x00003E99 File Offset: 0x00002099
    public IEnumerator StartBossfight()
    {
        MushroomHunter[] array = Object.FindObjectsOfType<MushroomHunter>();
        for (int i = 0; i < array.Length; i++)
        {
            array[i].TakeDamage(1000);
        }
        this.timerText.gameObject.SetActive(false);
        this.mushroomAnimator.SetBool("gameOver", true);
        yield return new WaitForSeconds(2f);
        this.player.transform.position = this.posBeforeBossfight.position;
        this.player.transform.rotation = this.posBeforeBossfight.rotation;
        this.player.enabled = false;
        yield return new WaitForSeconds(2f);
        this.fenceAnimator.SetBool("bossfightStarted", true);
        yield return new WaitForSeconds(4f);
        this.bossHP.SetActive(true);
        Object.Instantiate<GameObject>(this.boss, this.bossSpawnPos.position, Quaternion.identity);
        this.player.enabled = true;
        base.enabled = false;
        yield break;
    }

    // Token: 0x0400007A RID: 122
    public GameObject[] enemies;

    // Token: 0x0400007B RID: 123
    public Transform[] spawnPoints;

    // Token: 0x0400007C RID: 124
    public TextMeshProUGUI timerText;

    // Token: 0x0400007D RID: 125
    public PlayerController player;

    // Token: 0x0400007E RID: 126
    public Animator mushroomAnimator;

    // Token: 0x0400007F RID: 127
    public Animator fenceAnimator;

    // Token: 0x04000080 RID: 128
    public GameObject boss;

    // Token: 0x04000081 RID: 129
    public GameObject bossHP;

    // Token: 0x04000082 RID: 130
    public Transform posBeforeBossfight;

    // Token: 0x04000083 RID: 131
    public Transform bossSpawnPos;

    // Token: 0x04000084 RID: 132
    private float gameTime;

    // Token: 0x04000085 RID: 133
    public float startGameTime;

    // Token: 0x04000086 RID: 134
    private float timeBetweenSpawn;

    // Token: 0x04000087 RID: 135
    public float startTimeBetweenSpawn;

    // Token: 0x04000088 RID: 136
    public float decreaseTime;

    // Token: 0x04000089 RID: 137
    public float minTime;

    // Token: 0x0400008A RID: 138
    private bool isReady;

    // Token: 0x0400008B RID: 139
    private bool alreadyStarted;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x0200001D RID: 29
public class MushroomHunter : MonoBehaviour
{
    // Token: 0x06000083 RID: 131 RVA: 0x00003EB0 File Offset: 0x000020B0
    private void Awake()
    {
        this.agent = base.GetComponent<NavMeshAgent>();
    }

    // Token: 0x06000084 RID: 132 RVA: 0x00003EBE File Offset: 0x000020BE
    private void Start()
    {
        this.walkPoint = base.transform.position;
    }

    // Token: 0x06000085 RID: 133 RVA: 0x00003ED4 File Offset: 0x000020D4
    private void Update()
    {
        this.FindClosestMushroom();
        this.mushroomInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsMushroom);
        this.mushroomInPickRange = Physics.CheckSphere(base.transform.position, this.pickRange, this.whatIsMushroom);
        if (!this.hasPicked)
        {
            if (!this.mushroomInSightRange && !this.mushroomInPickRange)
            {
                this.Patroling();
            }
            if (this.mushroomInSightRange && !this.mushroomInPickRange)
            {
                this.ChaseMushroom();
            }
            if (this.mushroomInPickRange && this.mushroomInSightRange)
            {
                this.PickMushroom();
                return;
            }
        }
        else
        {
            this.Patroling();
        }
    }

    // Token: 0x06000086 RID: 134 RVA: 0x00003F88 File Offset: 0x00002188
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 5f)
        {
            this.walkPointSet = false;
            if (this.hasPicked)
            {
                this.hasPicked = false;
                this.DestroyEnemy();
            }
        }
    }

    // Token: 0x06000087 RID: 135 RVA: 0x00004000 File Offset: 0x00002200
    public void FindClosestMushroom()
    {
        float num = float.PositiveInfinity;
        foreach (GameObject gameObject in GameObject.FindGameObjectsWithTag("CentralMushroom"))
        {
            float sqrMagnitude = (gameObject.transform.position - base.transform.position).sqrMagnitude;
            if (sqrMagnitude < num)
            {
                num = sqrMagnitude;
                this.closestMushroom = gameObject.transform;
            }
        }
    }

    // Token: 0x06000088 RID: 136 RVA: 0x0000406A File Offset: 0x0000226A
    private void SearchWalkPoint()
    {
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x06000089 RID: 137 RVA: 0x000040A0 File Offset: 0x000022A0
    private void ChaseMushroom()
    {
        this.agent.SetDestination(this.closestMushroom.position);
    }

    // Token: 0x0600008A RID: 138 RVA: 0x000040BC File Offset: 0x000022BC
    private void PickMushroom()
    {
        this.hasPicked = true;
        this.closestMushroom.transform.parent = this.mushroomPos;
        this.closestMushroom.transform.position = this.mushroomPos.position;
        this.closestMushroom.transform.rotation = this.mushroomPos.rotation;
    }

    // Token: 0x0600008B RID: 139 RVA: 0x0000411C File Offset: 0x0000231C
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            this.DestroyEnemy();
        }
    }

    // Token: 0x0600008C RID: 140 RVA: 0x00004140 File Offset: 0x00002340
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        if (this.hasPicked)
        {
            Object.Instantiate<GameObject>(this.newMushroom, base.transform.position, Quaternion.identity);
        }
        Object.Destroy(base.gameObject);
    }

    // Token: 0x0600008D RID: 141 RVA: 0x00004198 File Offset: 0x00002398
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.pickRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x0400008C RID: 140
    public NavMeshAgent agent;

    // Token: 0x0400008D RID: 141
    public Transform mushroomPos;

    // Token: 0x0400008E RID: 142
    public Transform closestMushroom;

    // Token: 0x0400008F RID: 143
    public LayerMask whatIsGround;

    // Token: 0x04000090 RID: 144
    public LayerMask whatIsMushroom;

    // Token: 0x04000091 RID: 145
    public Vector3 walkPoint;

    // Token: 0x04000092 RID: 146
    private bool walkPointSet;

    // Token: 0x04000093 RID: 147
    public float walkPointRange;

    // Token: 0x04000094 RID: 148
    public float sightRange;

    // Token: 0x04000095 RID: 149
    public float pickRange;

    // Token: 0x04000096 RID: 150
    public bool mushroomInSightRange;

    // Token: 0x04000097 RID: 151
    public bool mushroomInPickRange;

    // Token: 0x04000098 RID: 152
    public bool hasPicked;

    // Token: 0x04000099 RID: 153
    public float health;

    // Token: 0x0400009A RID: 154
    public int damage;

    // Token: 0x0400009B RID: 155
    public GameObject deathEffect;

    // Token: 0x0400009C RID: 156
    public GameObject newMushroom;
}

using System;
using UnityEngine;

// Token: 0x0200001E RID: 30
public class MusicManager : MonoBehaviour
{
    // Token: 0x0600008F RID: 143 RVA: 0x000041ED File Offset: 0x000023ED
    private void Awake()
    {
        if (MusicManager.music != null)
        {
            Object.Destroy(base.gameObject);
            return;
        }
        MusicManager.music = this;
        Object.DontDestroyOnLoad(base.transform.gameObject);
    }

    // Token: 0x06000090 RID: 144 RVA: 0x0000421E File Offset: 0x0000241E
    private void Start()
    {
        this.musicVariants[0].Play();
    }

    // Token: 0x06000091 RID: 145 RVA: 0x00004230 File Offset: 0x00002430
    public void EnableMusic(int musicIndex)
    {
        if (!this.musicVariants[musicIndex].isPlaying)
        {
            AudioSource[] array = this.musicVariants;
            for (int i = 0; i < array.Length; i++)
            {
                array[i].Stop();
            }
            this.musicVariants[musicIndex].Play();
        }
    }

    // Token: 0x0400009D RID: 157
    private static MusicManager music;

    // Token: 0x0400009E RID: 158
    public AudioSource[] musicVariants;
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Token: 0x02000005 RID: 5
[DisallowMultipleComponent]
public class Outline : MonoBehaviour
{
    // Token: 0x17000001 RID: 1
    // (get) Token: 0x06000009 RID: 9 RVA: 0x00002394 File Offset: 0x00000594
    // (set) Token: 0x0600000A RID: 10 RVA: 0x0000239C File Offset: 0x0000059C
    public Outline.Mode OutlineMode
    {
        get
        {
            return this.outlineMode;
        }
        set
        {
            this.outlineMode = value;
            this.needsUpdate = true;
        }
    }

    // Token: 0x17000002 RID: 2
    // (get) Token: 0x0600000B RID: 11 RVA: 0x000023AC File Offset: 0x000005AC
    // (set) Token: 0x0600000C RID: 12 RVA: 0x000023B4 File Offset: 0x000005B4
    public Color OutlineColor
    {
        get
        {
            return this.outlineColor;
        }
        set
        {
            this.outlineColor = value;
            this.needsUpdate = true;
        }
    }

    // Token: 0x17000003 RID: 3
    // (get) Token: 0x0600000D RID: 13 RVA: 0x000023C4 File Offset: 0x000005C4
    // (set) Token: 0x0600000E RID: 14 RVA: 0x000023CC File Offset: 0x000005CC
    public float OutlineWidth
    {
        get
        {
            return this.outlineWidth;
        }
        set
        {
            this.outlineWidth = value;
            this.needsUpdate = true;
        }
    }

    // Token: 0x0600000F RID: 15 RVA: 0x000023DC File Offset: 0x000005DC
    private void Awake()
    {
        this.renderers = base.GetComponentsInChildren<Renderer>();
        this.outlineMaskMaterial = Object.Instantiate<Material>(Resources.Load<Material>("Materials/OutlineMask"));
        this.outlineFillMaterial = Object.Instantiate<Material>(Resources.Load<Material>("Materials/OutlineFill"));
        this.outlineMaskMaterial.name = "OutlineMask (Instance)";
        this.outlineFillMaterial.name = "OutlineFill (Instance)";
        this.LoadSmoothNormals();
        this.needsUpdate = true;
    }

    // Token: 0x06000010 RID: 16 RVA: 0x0000244C File Offset: 0x0000064C
    private void OnEnable()
    {
        foreach (Renderer renderer in this.renderers)
        {
            List<Material> list = renderer.sharedMaterials.ToList<Material>();
            list.Add(this.outlineMaskMaterial);
            list.Add(this.outlineFillMaterial);
            renderer.materials = list.ToArray();
        }
    }

    // Token: 0x06000011 RID: 17 RVA: 0x000024A0 File Offset: 0x000006A0
    private void OnMouseOver()
    {
        this.outlineWidth = 10f;
        this.needsUpdate = true;
    }

    // Token: 0x06000012 RID: 18 RVA: 0x000024B4 File Offset: 0x000006B4
    private void OnMouseExit()
    {
        this.outlineWidth = 0f;
        this.needsUpdate = true;
    }

    // Token: 0x06000013 RID: 19 RVA: 0x000024C8 File Offset: 0x000006C8
    private void OnValidate()
    {
        this.needsUpdate = true;
        if ((!this.precomputeOutline && this.bakeKeys.Count != 0) || this.bakeKeys.Count != this.bakeValues.Count)
        {
            this.bakeKeys.Clear();
            this.bakeValues.Clear();
        }
        if (this.precomputeOutline && this.bakeKeys.Count == 0)
        {
            this.Bake();
        }
    }

    // Token: 0x06000014 RID: 20 RVA: 0x0000253A File Offset: 0x0000073A
    private void Update()
    {
        if (this.needsUpdate)
        {
            this.needsUpdate = false;
            this.UpdateMaterialProperties();
        }
    }

    // Token: 0x06000015 RID: 21 RVA: 0x00002554 File Offset: 0x00000754
    private void OnDisable()
    {
        foreach (Renderer renderer in this.renderers)
        {
            List<Material> list = renderer.sharedMaterials.ToList<Material>();
            list.Remove(this.outlineMaskMaterial);
            list.Remove(this.outlineFillMaterial);
            renderer.materials = list.ToArray();
        }
    }

    // Token: 0x06000016 RID: 22 RVA: 0x000025AA File Offset: 0x000007AA
    private void OnDestroy()
    {
        Object.Destroy(this.outlineMaskMaterial);
        Object.Destroy(this.outlineFillMaterial);
    }

    // Token: 0x06000017 RID: 23 RVA: 0x000025C4 File Offset: 0x000007C4
    private void Bake()
    {
        HashSet<Mesh> hashSet = new HashSet<Mesh>();
        foreach (MeshFilter meshFilter in base.GetComponentsInChildren<MeshFilter>())
        {
            if (hashSet.Add(meshFilter.sharedMesh))
            {
                List<Vector3> data = this.SmoothNormals(meshFilter.sharedMesh);
                this.bakeKeys.Add(meshFilter.sharedMesh);
                this.bakeValues.Add(new Outline.ListVector3
                {
                    data = data
                });
            }
        }
    }

    // Token: 0x06000018 RID: 24 RVA: 0x00002638 File Offset: 0x00000838
    private void LoadSmoothNormals()
    {
        foreach (MeshFilter meshFilter in base.GetComponentsInChildren<MeshFilter>())
        {
            if (Outline.registeredMeshes.Add(meshFilter.sharedMesh))
            {
                int num = this.bakeKeys.IndexOf(meshFilter.sharedMesh);
                List<Vector3> uvs = (num >= 0) ? this.bakeValues[num].data : this.SmoothNormals(meshFilter.sharedMesh);
                meshFilter.sharedMesh.SetUVs(3, uvs);
                Renderer component = meshFilter.GetComponent<Renderer>();
                if (component != null)
                {
                    this.CombineSubmeshes(meshFilter.sharedMesh, component.sharedMaterials);
                }
            }
        }
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in base.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (Outline.registeredMeshes.Add(skinnedMeshRenderer.sharedMesh))
            {
                skinnedMeshRenderer.sharedMesh.uv4 = new Vector2[skinnedMeshRenderer.sharedMesh.vertexCount];
                this.CombineSubmeshes(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials);
            }
        }
    }

    // Token: 0x06000019 RID: 25 RVA: 0x00002744 File Offset: 0x00000944
    private List<Vector3> SmoothNormals(Mesh mesh)
    {
        IEnumerable<IGrouping<Vector3, KeyValuePair<Vector3, int>>> enumerable = from pair in mesh.vertices.Select((Vector3 vertex, int index) => new KeyValuePair<Vector3, int>(vertex, index))
                                                                                 group pair by pair.Key;
        List<Vector3> list = new List<Vector3>(mesh.normals);
        foreach (IGrouping<Vector3, KeyValuePair<Vector3, int>> grouping in enumerable)
        {
            if (grouping.Count<KeyValuePair<Vector3, int>>() != 1)
            {
                Vector3 vector = Vector3.zero;
                foreach (KeyValuePair<Vector3, int> keyValuePair in grouping)
                {
                    vector += list[keyValuePair.Value];
                }
                vector.Normalize();
                foreach (KeyValuePair<Vector3, int> keyValuePair2 in grouping)
                {
                    list[keyValuePair2.Value] = vector;
                }
            }
        }
        return list;
    }

    // Token: 0x0600001A RID: 26 RVA: 0x0000288C File Offset: 0x00000A8C
    private void CombineSubmeshes(Mesh mesh, Material[] materials)
    {
        if (mesh.subMeshCount == 1)
        {
            return;
        }
        if (mesh.subMeshCount > materials.Length)
        {
            return;
        }
        int subMeshCount = mesh.subMeshCount;
        mesh.subMeshCount = subMeshCount + 1;
        mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1);
    }

    // Token: 0x0600001B RID: 27 RVA: 0x000028D4 File Offset: 0x00000AD4
    private void UpdateMaterialProperties()
    {
        this.outlineFillMaterial.SetColor("_OutlineColor", this.outlineColor);
        switch (this.outlineMode)
        {
            case Outline.Mode.OutlineAll:
                this.outlineMaskMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.OutlineVisible:
                this.outlineMaskMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_ZTest", 4f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.OutlineHidden:
                this.outlineMaskMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_ZTest", 5f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.OutlineAndSilhouette:
                this.outlineMaskMaterial.SetFloat("_ZTest", 4f);
                this.outlineFillMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.SilhouetteOnly:
                this.outlineMaskMaterial.SetFloat("_ZTest", 4f);
                this.outlineFillMaterial.SetFloat("_ZTest", 5f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", 0f);
                return;
            default:
                return;
        }
    }

    // Token: 0x04000014 RID: 20
    private static HashSet<Mesh> registeredMeshes = new HashSet<Mesh>();

    // Token: 0x04000015 RID: 21
    [SerializeField]
    private Outline.Mode outlineMode;

    // Token: 0x04000016 RID: 22
    [SerializeField]
    private Color outlineColor = Color.white;

    // Token: 0x04000017 RID: 23
    [SerializeField]
    [Range(0f, 10f)]
    private float outlineWidth = 2f;

    // Token: 0x04000018 RID: 24
    [Header("Optional")]
    [SerializeField]
    [Tooltip("Precompute enabled: Per-vertex calculations are performed in the editor and serialized with the object. Precompute disabled: Per-vertex calculations are performed at runtime in Awake(). This may cause a pause for large meshes.")]
    private bool precomputeOutline;

    // Token: 0x04000019 RID: 25
    [SerializeField]
    [HideInInspector]
    private List<Mesh> bakeKeys = new List<Mesh>();

    // Token: 0x0400001A RID: 26
    [SerializeField]
    [HideInInspector]
    private List<Outline.ListVector3> bakeValues = new List<Outline.ListVector3>();

    // Token: 0x0400001B RID: 27
    private Renderer[] renderers;

    // Token: 0x0400001C RID: 28
    private Material outlineMaskMaterial;

    // Token: 0x0400001D RID: 29
    private Material outlineFillMaterial;

    // Token: 0x0400001E RID: 30
    private bool needsUpdate;

    // Token: 0x0200005A RID: 90
    public enum Mode
    {
        // Token: 0x040002B3 RID: 691
        OutlineAll,
        // Token: 0x040002B4 RID: 692
        OutlineVisible,
        // Token: 0x040002B5 RID: 693
        OutlineHidden,
        // Token: 0x040002B6 RID: 694
        OutlineAndSilhouette,
        // Token: 0x040002B7 RID: 695
        SilhouetteOnly
    }

    // Token: 0x0200005B RID: 91
    [Serializable]
    private class ListVector3
    {
        // Token: 0x040002B8 RID: 696
        public List<Vector3> data;
    }
}

using System;
using UnityEngine;

// Token: 0x0200001F RID: 31
public class PauseMenu : MonoBehaviour
{
    // Token: 0x06000093 RID: 147 RVA: 0x0000427E File Offset: 0x0000247E
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!this.pausePanel.activeInHierarchy)
            {
                this.OnPause();
                return;
            }
            this.OnResume();
        }
    }

    // Token: 0x06000094 RID: 148 RVA: 0x000042A3 File Offset: 0x000024A3
    public void OnPause()
    {
        this.pausePanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
    }

    // Token: 0x06000095 RID: 149 RVA: 0x000042C1 File Offset: 0x000024C1
    public void OnResume()
    {
        this.pausePanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Time.timeScale = 1f;
    }

    // Token: 0x0400009F RID: 159
    public GameObject pausePanel;
}

using System;
using UnityEngine;

// Token: 0x02000020 RID: 32
public class PlayerCamera : MonoBehaviour
{
    // Token: 0x06000097 RID: 151 RVA: 0x000042E7 File Offset: 0x000024E7
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Token: 0x06000098 RID: 152 RVA: 0x000042F0 File Offset: 0x000024F0
    private void Update()
    {
        float num = Input.GetAxisRaw("Mouse X") * Time.deltaTime * this.sensitivityX;
        float num2 = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * this.sensitivityY;
        this.yRotation += num;
        this.xRotation -= num2;
        this.xRotation = Mathf.Clamp(this.xRotation, -90f, 90f);
        base.transform.rotation = Quaternion.Euler(this.xRotation, this.yRotation, 0f);
        this.orientation.rotation = Quaternion.Euler(0f, this.yRotation, 0f);
    }

    // Token: 0x040000A0 RID: 160
    public float sensitivityX;

    // Token: 0x040000A1 RID: 161
    public float sensitivityY;

    // Token: 0x040000A2 RID: 162
    public Transform orientation;

    // Token: 0x040000A3 RID: 163
    public Transform flashlight;

    // Token: 0x040000A4 RID: 164
    private float xRotation;

    // Token: 0x040000A5 RID: 165
    private float yRotation;
}

using System;
using UnityEngine;

// Token: 0x02000021 RID: 33
public class PlayerController : MonoBehaviour
{
    // Token: 0x0600009A RID: 154 RVA: 0x000043AD File Offset: 0x000025AD
    private void Start()
    {
        this.rb = base.GetComponent<Rigidbody>();
        this.rb.freezeRotation = true;
        this.readyToJump = true;
        this.LoadPosition();
    }

    // Token: 0x0600009B RID: 155 RVA: 0x000043D4 File Offset: 0x000025D4
    private void Update()
    {
        this.grounded = Physics.Raycast(base.transform.position, Vector3.down, this.groundedValue * 0.5f + 0.01f, this.whatIsGround);
        this.MyInput();
        this.SpeedControl();
        if (this.grounded)
        {
            this.rb.drag = this.groundDrag;
            return;
        }
        this.rb.drag = 0f;
    }

    // Token: 0x0600009C RID: 156 RVA: 0x0000444F File Offset: 0x0000264F
    private void FixedUpdate()
    {
        this.MovePlayer();
    }

    // Token: 0x0600009D RID: 157 RVA: 0x00004458 File Offset: 0x00002658
    private void MyInput()
    {
        this.horizontalInput = Input.GetAxisRaw("Horizontal");
        this.verticalInput = Input.GetAxisRaw("Vertical");
        if (Input.GetKey(this.jumpKey) && this.readyToJump && this.grounded)
        {
            this.readyToJump = false;
            this.Jump();
            base.Invoke("ResetJump", this.jumpCooldown);
        }
    }

    // Token: 0x0600009E RID: 158 RVA: 0x000044C0 File Offset: 0x000026C0
    private void MovePlayer()
    {
        this.moveDirection = this.orientation.forward * this.verticalInput + this.orientation.right * this.horizontalInput;
        if (this.grounded)
        {
            this.rb.AddForce(this.moveDirection.normalized * this.moveSpeed * 10f, ForceMode.Force);
            return;
        }
        if (!this.grounded)
        {
            this.rb.AddForce(this.moveDirection.normalized * this.moveSpeed * 10f * this.airMultiplier, ForceMode.Force);
        }
    }

    // Token: 0x0600009F RID: 159 RVA: 0x00004578 File Offset: 0x00002778
    private void SpeedControl()
    {
        Vector3 vector = new Vector3(this.rb.velocity.x, 0f, this.rb.velocity.z);
        if (vector.magnitude > this.moveSpeed)
        {
            Vector3 vector2 = vector.normalized * this.moveSpeed;
            this.rb.velocity = new Vector3(vector2.x, this.rb.velocity.y, vector2.z);
        }
    }

    // Token: 0x060000A0 RID: 160 RVA: 0x00004600 File Offset: 0x00002800
    private void Jump()
    {
        this.rb.velocity = new Vector3(this.rb.velocity.x, 0f, this.rb.velocity.z);
        this.rb.AddForce(base.transform.up * this.jumpForce, ForceMode.Impulse);
    }

    // Token: 0x060000A1 RID: 161 RVA: 0x00004664 File Offset: 0x00002864
    private void ResetJump()
    {
        this.readyToJump = true;
    }

    // Token: 0x060000A2 RID: 162 RVA: 0x00004670 File Offset: 0x00002870
    public void SavePosition()
    {
        this.x = base.transform.position.x;
        this.y = base.transform.position.y;
        this.z = base.transform.position.z;
        PlayerPrefs.SetFloat("x", this.x);
        PlayerPrefs.SetFloat("y", this.y);
        PlayerPrefs.SetFloat("z", this.z);
    }

    // Token: 0x060000A3 RID: 163 RVA: 0x000046F0 File Offset: 0x000028F0
    public void LoadPosition()
    {
        this.x = PlayerPrefs.GetFloat("x", 34.8f);
        this.y = PlayerPrefs.GetFloat("y", 0.7f);
        this.z = PlayerPrefs.GetFloat("z", 145.6f);
        Vector3 position = new Vector3(this.x, this.y, this.z);
        base.transform.position = position;
    }

    // Token: 0x040000A6 RID: 166
    [Header("Movement")]
    public float moveSpeed;

    // Token: 0x040000A7 RID: 167
    public Transform orientation;

    // Token: 0x040000A8 RID: 168
    public float groundDrag;

    // Token: 0x040000A9 RID: 169
    public float groundedValue;

    // Token: 0x040000AA RID: 170
    public float jumpForce;

    // Token: 0x040000AB RID: 171
    public float jumpCooldown;

    // Token: 0x040000AC RID: 172
    public float airMultiplier;

    // Token: 0x040000AD RID: 173
    public float x;

    // Token: 0x040000AE RID: 174
    public float y;

    // Token: 0x040000AF RID: 175
    public float z;

    // Token: 0x040000B0 RID: 176
    private bool readyToJump;

    // Token: 0x040000B1 RID: 177
    [Header("Ground Check")]
    public float playerHeight;

    // Token: 0x040000B2 RID: 178
    public LayerMask whatIsGround;

    // Token: 0x040000B3 RID: 179
    public bool grounded;

    // Token: 0x040000B4 RID: 180
    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;

    // Token: 0x040000B5 RID: 181
    private float horizontalInput;

    // Token: 0x040000B6 RID: 182
    private float verticalInput;

    // Token: 0x040000B7 RID: 183
    private Vector3 moveDirection;

    // Token: 0x040000B8 RID: 184
    private Rigidbody rb;
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Token: 0x02000022 RID: 34
public class PlayerHealth : MonoBehaviour
{
    // Token: 0x060000A5 RID: 165 RVA: 0x00004771 File Offset: 0x00002971
    private void Start()
    {
        this.currentHealth = this.maxHealth;
        this.currectHealCooldown = this.healCooldownAfterDamage;
    }

    // Token: 0x060000A6 RID: 166 RVA: 0x0000478C File Offset: 0x0000298C
    private void Update()
    {
        this.bloodOverlay.color = new Color(this.bloodOverlay.color.r, this.bloodOverlay.color.g, this.bloodOverlay.color.b, 1f - this.currentHealth / this.maxHealth);
        if (this.currectHealCooldown > 0f)
        {
            this.currectHealCooldown -= Time.deltaTime;
        }
        else
        {
            this.canHeal = true;
        }
        if (Time.realtimeSinceStartup < this.nextHealthUpdateTime)
        {
            return;
        }
        this.nextHealthUpdateTime = Time.realtimeSinceStartup + this.healSpeed;
        if (this.currentHealth < this.maxHealth && this.canHeal)
        {
            this.currentHealth += this.healAmount;
        }
    }

    // Token: 0x060000A7 RID: 167 RVA: 0x00004860 File Offset: 0x00002A60
    public void TakeDamage(float damage)
    {
        this.canHeal = false;
        this.currectHealCooldown = this.healCooldownAfterDamage;
        this.currentHealth -= damage;
        if (this.currentHealth <= 0f)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // Token: 0x060000A8 RID: 168 RVA: 0x000048AD File Offset: 0x00002AAD
    private void OnParticleCollision(GameObject other)
    {
        this.TakeDamage(1f);
    }

    // Token: 0x040000B9 RID: 185
    public Image bloodOverlay;

    // Token: 0x040000BA RID: 186
    public float maxHealth = 100f;

    // Token: 0x040000BB RID: 187
    public float currentHealth = 100f;

    // Token: 0x040000BC RID: 188
    private float nextHealthUpdateTime;

    // Token: 0x040000BD RID: 189
    public float healSpeed = 1f;

    // Token: 0x040000BE RID: 190
    public float healAmount = 1f;

    // Token: 0x040000BF RID: 191
    public float healCooldownAfterDamage = 5f;

    // Token: 0x040000C0 RID: 192
    public float currectHealCooldown;

    // Token: 0x040000C1 RID: 193
    private bool canHeal = true;
}

using System;
using UnityEngine;

// Token: 0x02000004 RID: 4
public class PlayerMovement : MonoBehaviour
{
    // Token: 0x06000007 RID: 7 RVA: 0x00002208 File Offset: 0x00000408
    private void Update()
    {
        this.isGrounded = Physics.CheckSphere(this.groundCheck.position, this.groundDistance, this.groundMask);
        if (this.isGrounded && this.velocity.y < 0f)
        {
            this.velocity.y = -2f;
        }
        if (Input.GetKey("left shift") && this.isGrounded)
        {
            this.speed = 10f;
        }
        else
        {
            this.speed = 5f;
        }
        float axis = Input.GetAxis("Horizontal");
        float axis2 = Input.GetAxis("Vertical");
        Vector3 a = base.transform.right * axis + base.transform.forward * axis2;
        this.controller.Move(a * this.speed * Time.deltaTime);
        if (Input.GetButtonDown("Jump") && this.isGrounded)
        {
            this.velocity.y = Mathf.Sqrt(this.jumpHeight * -2f * this.gravity);
        }
        this.velocity.y = this.velocity.y + this.gravity * Time.deltaTime;
        this.controller.Move(this.velocity * Time.deltaTime);
    }

    // Token: 0x0400000B RID: 11
    public CharacterController controller;

    // Token: 0x0400000C RID: 12
    public float speed = 5f;

    // Token: 0x0400000D RID: 13
    public float gravity = -9.18f;

    // Token: 0x0400000E RID: 14
    public float jumpHeight = 3f;

    // Token: 0x0400000F RID: 15
    public Transform groundCheck;

    // Token: 0x04000010 RID: 16
    public float groundDistance = 0.4f;

    // Token: 0x04000011 RID: 17
    public LayerMask groundMask;

    // Token: 0x04000012 RID: 18
    private Vector3 velocity;

    // Token: 0x04000013 RID: 19
    private bool isGrounded;
}

using System;
using UnityEngine;

// Token: 0x02000023 RID: 35
public class ProjectileAddon : MonoBehaviour
{
    // Token: 0x060000AA RID: 170 RVA: 0x0000490D File Offset: 0x00002B0D
    private void Start()
    {
        this.rb = base.GetComponent<Rigidbody>();
    }

    // Token: 0x060000AB RID: 171 RVA: 0x0000491C File Offset: 0x00002B1C
    private void OnCollisionEnter(Collision collision)
    {
        if (this.targetHit)
        {
            return;
        }
        this.targetHit = true;
        if (collision.gameObject.GetComponent<BasicEnemy>() != null)
        {
            collision.gameObject.GetComponent<BasicEnemy>().TakeDamage(this.damage);
            Object.Destroy(base.gameObject);
        }
        this.rb.isKinematic = true;
        base.transform.SetParent(collision.transform);
    }

    // Token: 0x040000C2 RID: 194
    public int damage;

    // Token: 0x040000C3 RID: 195
    private Rigidbody rb;

    // Token: 0x040000C4 RID: 196
    private bool targetHit;
}

using System;
using UnityEngine;

// Token: 0x02000024 RID: 36
public class RunOver : MonoBehaviour
{
    // Token: 0x060000AD RID: 173 RVA: 0x00004992 File Offset: 0x00002B92
    private void Start()
    {
        this.gateAnimator.SetBool("hasMushrooms", true);
    }

    // Token: 0x060000AE RID: 174 RVA: 0x000049A5 File Offset: 0x00002BA5
    private void Update()
    {
        this.hasFinishedRun = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        if (this.hasFinishedRun)
        {
            this.EndRun();
        }
    }

    // Token: 0x060000AF RID: 175 RVA: 0x000049DC File Offset: 0x00002BDC
    public void EndRun()
    {
        this.enemyRun.TakeDamage(50);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.FindObjectOfType<PlayerController>().moveSpeed = 3.5f;
        this.gateAnimator.SetBool("pastGate", true);
        base.enabled = false;
    }

    // Token: 0x060000B0 RID: 176 RVA: 0x00004A28 File Offset: 0x00002C28
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x040000C5 RID: 197
    public Animator gateAnimator;

    // Token: 0x040000C6 RID: 198
    public Enemy enemyRun;

    // Token: 0x040000C7 RID: 199
    public LayerMask whatIsPlayer;

    // Token: 0x040000C8 RID: 200
    public float sightRange;

    // Token: 0x040000C9 RID: 201
    private bool hasFinishedRun;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000025 RID: 37
public class SaveDropdownValue : MonoBehaviour
{
    // Token: 0x060000B2 RID: 178 RVA: 0x00004A52 File Offset: 0x00002C52
    private void Awake()
    {
        this.dropdown = base.GetComponent<TMP_Dropdown>();
        this.dropdown.onValueChanged.AddListener(delegate (int index)
        {
            PlayerPrefs.SetInt("optionvalue", this.dropdown.value);
            PlayerPrefs.Save();
        });
    }

    // Token: 0x060000B3 RID: 179 RVA: 0x00004A7C File Offset: 0x00002C7C
    private void Start()
    {
        this.dropdown.value = PlayerPrefs.GetInt("optionvalue", 2);
    }

    // Token: 0x040000CA RID: 202
    private const string PrefName = "optionvalue";

    // Token: 0x040000CB RID: 203
    private TMP_Dropdown dropdown;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000026 RID: 38
public class SetQuality : MonoBehaviour
{
    // Token: 0x060000B6 RID: 182 RVA: 0x00004AB8 File Offset: 0x00002CB8
    public void SetQualityLevel(int index)
    {
        QualitySettings.SetQualityLevel(index, false);
        this.settings.SaveQuality(index);
    }

    // Token: 0x040000CC RID: 204
    [SerializeField]
    private TMP_Dropdown qualityDropdown;

    // Token: 0x040000CD RID: 205
    [SerializeField]
    private GameSettings settings;
}

using System;
using UnityEngine;

// Token: 0x02000027 RID: 39
public class SmallChanceSpawner : MonoBehaviour
{
    // Token: 0x060000B8 RID: 184 RVA: 0x00004AD5 File Offset: 0x00002CD5
    private void Start()
    {
        if (Random.Range(0, 5) == 0)
        {
            Object.Instantiate<GameObject>(this.spawnable, base.transform.position, Quaternion.identity);
        }
    }

    // Token: 0x040000CE RID: 206
    public GameObject spawnable;
}

using System;
using UnityEngine;

// Token: 0x02000028 RID: 40
public class SoundManager : MonoBehaviour
{
    // Token: 0x060000BA RID: 186 RVA: 0x00004B04 File Offset: 0x00002D04
    private void Start()
    {
        if (this.isEnemy)
        {
            this.target = base.transform;
        }
        else
        {
            this.target = Object.FindObjectOfType<PlayerController>().transform;
        }
        this.currentTimeBetweenSounds = this.NewSoundCooldown();
    }

    // Token: 0x060000BB RID: 187 RVA: 0x00004B38 File Offset: 0x00002D38
    public void Update()
    {
        if (this.soundEffects.Length != 0)
        {
            if (this.currentTimeBetweenSounds <= 0f)
            {
                Object.Instantiate<GameObject>(this.soundEffects[Random.Range(0, this.soundEffects.Length)], this.target.position, Quaternion.identity);
                this.currentTimeBetweenSounds = this.NewSoundCooldown();
                return;
            }
            this.currentTimeBetweenSounds -= Time.deltaTime;
        }
    }

    // Token: 0x060000BC RID: 188 RVA: 0x00004BA5 File Offset: 0x00002DA5
    public float NewSoundCooldown()
    {
        return Random.Range(this.minSecondsBetweenSounds, this.maxSecondsBetweenSounds);
    }

    // Token: 0x040000CF RID: 207
    public GameObject[] soundEffects;

    // Token: 0x040000D0 RID: 208
    public float minSecondsBetweenSounds;

    // Token: 0x040000D1 RID: 209
    public float maxSecondsBetweenSounds;

    // Token: 0x040000D2 RID: 210
    public float currentTimeBetweenSounds;

    // Token: 0x040000D3 RID: 211
    public bool isEnemy;

    // Token: 0x040000D4 RID: 212
    private Transform target;
}

using System;
using UnityEngine;

// Token: 0x02000029 RID: 41
public class SparksBullet : MonoBehaviour
{
    // Token: 0x060000BE RID: 190 RVA: 0x00004BC0 File Offset: 0x00002DC0
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            if (other.transform.root.GetComponent<Enemy>() != null)
            {
                other.transform.root.GetComponent<Enemy>().TakeDamage(this.bulletDamage);
                Object.Destroy(base.gameObject);
                return;
            }
            if (other.transform.root.GetComponent<MushroomHunter>() != null)
            {
                other.transform.root.GetComponent<MushroomHunter>().TakeDamage(this.bulletDamage);
                Object.Destroy(base.gameObject);
                return;
            }
            if (other.transform.root.GetComponent<Boss>() != null)
            {
                other.transform.root.GetComponent<Boss>().TakeDamage(this.bulletDamage);
                Object.Destroy(base.gameObject);
                return;
            }
        }
        else
        {
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x060000BF RID: 191 RVA: 0x00004CA5 File Offset: 0x00002EA5
    private void OnDestroy()
    {
        Object.Instantiate<GameObject>(this.fireBall, base.transform.position, Quaternion.identity);
    }

    // Token: 0x040000D5 RID: 213
    public int bulletDamage;

    // Token: 0x040000D6 RID: 214
    public GameObject fireBall;
}

using System;
using UnityEngine;

// Token: 0x02000002 RID: 2
public class SpawnEffect : MonoBehaviour
{
    // Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
    private void Start()
    {
        this.shaderProperty = Shader.PropertyToID("_cutoff");
        this._renderer = base.GetComponent<Renderer>();
        this.ps = base.GetComponentInChildren<ParticleSystem>();
        this.ps.main.duration = this.spawnEffectTime;
        this.ps.Play();
    }

    // Token: 0x06000002 RID: 2 RVA: 0x000020AC File Offset: 0x000002AC
    private void Update()
    {
        if (this.timer < this.spawnEffectTime + this.pause)
        {
            this.timer += Time.deltaTime;
        }
        else
        {
            this.ps.Play();
            this.timer = 0f;
        }
        this._renderer.material.SetFloat(this.shaderProperty, this.fadeIn.Evaluate(Mathf.InverseLerp(0f, this.spawnEffectTime, this.timer)));
    }

    // Token: 0x04000001 RID: 1
    public float spawnEffectTime = 2f;

    // Token: 0x04000002 RID: 2
    public float pause = 1f;

    // Token: 0x04000003 RID: 3
    public AnimationCurve fadeIn;

    // Token: 0x04000004 RID: 4
    private ParticleSystem ps;

    // Token: 0x04000005 RID: 5
    private float timer;

    // Token: 0x04000006 RID: 6
    private Renderer _renderer;

    // Token: 0x04000007 RID: 7
    private int shaderProperty;
}

using System;
using UnityEngine;

// Token: 0x0200002A RID: 42
public class ThrowingTutorial : MonoBehaviour
{
    // Token: 0x060000C1 RID: 193 RVA: 0x00004CCB File Offset: 0x00002ECB
    private void Start()
    {
        this.readyToThrow = true;
    }

    // Token: 0x060000C2 RID: 194 RVA: 0x00004CD4 File Offset: 0x00002ED4
    private void Update()
    {
        if (this.readyToThrow && this.totalThrows > 0)
        {
            this.Throw();
        }
    }

    // Token: 0x060000C3 RID: 195 RVA: 0x00004CF0 File Offset: 0x00002EF0
    private void Throw()
    {
        this.readyToThrow = false;
        Rigidbody component = Object.Instantiate<GameObject>(this.objectToThrow, this.attackPoint.position, this.attackPoint.rotation).GetComponent<Rigidbody>();
        Vector3 a = this.attackPoint.transform.forward;
        RaycastHit raycastHit;
        if (Physics.Raycast(this.attackPoint.position, this.attackPoint.forward, out raycastHit, 500f))
        {
            a = (raycastHit.point - this.attackPoint.position).normalized;
        }
        Vector3 force = a * this.throwForce + base.transform.up * this.throwUpwardForce;
        component.AddForce(force, ForceMode.Impulse);
        this.totalThrows--;
        base.Invoke("ResetThrow", this.throwCooldown);
    }

    // Token: 0x060000C4 RID: 196 RVA: 0x00004DCD File Offset: 0x00002FCD
    private void ResetThrow()
    {
        this.readyToThrow = true;
    }

    // Token: 0x040000D7 RID: 215
    [Header("References")]
    public Transform attackPoint;

    // Token: 0x040000D8 RID: 216
    public GameObject objectToThrow;

    // Token: 0x040000D9 RID: 217
    [Header("Settings")]
    public int totalThrows;

    // Token: 0x040000DA RID: 218
    public float throwCooldown;

    // Token: 0x040000DB RID: 219
    [Header("Throwing")]
    public KeyCode throwKey = KeyCode.Mouse0;

    // Token: 0x040000DC RID: 220
    public float throwForce;

    // Token: 0x040000DD RID: 221
    public float throwUpwardForce;

    // Token: 0x040000DE RID: 222
    private bool readyToThrow;
}

using System;
using UnityEngine;

// Token: 0x0200002B RID: 43
public class WeaponController : MonoBehaviour
{
    // Token: 0x060000C6 RID: 198 RVA: 0x00004DE9 File Offset: 0x00002FE9
    private void Start()
    {
        this.weaponAnimator = base.GetComponent<Animator>();
    }

    // Token: 0x060000C7 RID: 199 RVA: 0x00004DF8 File Offset: 0x00002FF8
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            this.weaponAnimator.SetTrigger("isAttacking");
        }
        if (this.newSparks != null)
        {
            this.newSparks.transform.Translate(this.sparksDirection * (float)this.sparksSpeed * Time.deltaTime);
        }
    }

    // Token: 0x060000C8 RID: 200 RVA: 0x00004E58 File Offset: 0x00003058
    public void SpawnSparks()
    {
        this.newSparks = Object.Instantiate<GameObject>(this.sparksEffect, this.sparksPos.position, Quaternion.identity);
        this.sparksDirection = this.sparksPos.transform.forward;
        Object.Instantiate<GameObject>(this.sounds[Random.Range(0, this.sounds.Length)], this.sparksPos.position, Quaternion.identity);
    }

    // Token: 0x040000DF RID: 223
    public GameObject sparksEffect;

    // Token: 0x040000E0 RID: 224
    public GameObject newSparks;

    // Token: 0x040000E1 RID: 225
    public Transform sparksPos;

    // Token: 0x040000E2 RID: 226
    public int sparksSpeed;

    // Token: 0x040000E3 RID: 227
    public GameObject[] sounds;

    // Token: 0x040000E4 RID: 228
    private Animator weaponAnimator;

    // Token: 0x040000E5 RID: 229
    private Vector3 sparksDirection;
}

using System;
using UnityEngine;

// Token: 0x0200002C RID: 44
public class YT : MonoBehaviour
{
    // Token: 0x060000CA RID: 202 RVA: 0x00004ECF File Offset: 0x000030CF
    public void OnButtonDown()
    {
        Application.OpenURL("https://www.youtube.com/channel/UCmjNGJn0WNPJS_G52UfkH-g");
    }
}

// Остров Шайлушая

using System;

// Token: 0x02000001 RID: 1
internal class <Module>
{
}

using System;
using UnityEngine;

// Token: 0x02000028 RID: 40
public class AfterTitles : MonoBehaviour
{
    // Token: 0x060000E0 RID: 224 RVA: 0x00005178 File Offset: 0x00003378
    public void OnTitlesOver()
    {
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        if (this.spawner != null)
        {
            Object.Destroy(this.spawner);
        }
        PlayerPrefs.DeleteAll();
        CursorController.UnlockCursor();
        this.menu.OnExit();
    }

    // Token: 0x040000F6 RID: 246
    public MenuExit menu;

    // Token: 0x040000F7 RID: 247
    public GameObject spawner;
}

using System;

// Token: 0x02000079 RID: 121
[Serializable]
public enum AvatarPosition
{
    // Token: 0x040002C4 RID: 708
    None,
    // Token: 0x040002C5 RID: 709
    Left,
    // Token: 0x040002C6 RID: 710
    Right
}

using System;

// Token: 0x0200007A RID: 122
[Serializable]
public enum AvatarType
{
    // Token: 0x040002C8 RID: 712
    Normal,
    // Token: 0x040002C9 RID: 713
    Smile,
    // Token: 0x040002CA RID: 714
    Suprized,
    // Token: 0x040002CB RID: 715
    Disgust,
    // Token: 0x040002CC RID: 716
    Crying,
    // Token: 0x040002CD RID: 717
    Angry
}

using System;

// Token: 0x02000010 RID: 16
public enum AxisOptions
{
    // Token: 0x04000039 RID: 57
    Both,
    // Token: 0x0400003A RID: 58
    Horizontal,
    // Token: 0x0400003B RID: 59
    Vertical
}

using System;
using System.Collections;
using UnityEngine;

// Token: 0x02000002 RID: 2
public class BarrierDamageDealer : MonoBehaviour
{
    // Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.CompareTag("Player"))
        {
            this.playerInArea = true;
            this.warningText.SetActive(true);
            if (this.damageCoroutine == null)
            {
                this.damageCoroutine = base.StartCoroutine(this.ApplyDamage(other.transform.root.GetComponent<PlayerHealth>()));
            }
        }
    }

    // Token: 0x06000002 RID: 2 RVA: 0x000020B4 File Offset: 0x000002B4
    private void OnTriggerExit(Collider other)
    {
        if (other.transform.root.CompareTag("Player"))
        {
            this.playerInArea = false;
            this.warningText.SetActive(false);
            if (this.damageCoroutine != null)
            {
                base.StopCoroutine(this.damageCoroutine);
                this.damageCoroutine = null;
            }
        }
    }

    // Token: 0x06000003 RID: 3 RVA: 0x00002106 File Offset: 0x00000306
    private IEnumerator ApplyDamage(PlayerHealth player)
    {
        while (this.playerInArea)
        {
            if (player != null)
            {
                player.TakeDamage((float)this.damagePerSecond);
            }
            yield return new WaitForSeconds(this.damageInterval);
        }
        this.damageCoroutine = null;
        yield break;
    }

    // Token: 0x04000001 RID: 1
    public GameObject warningText;

    // Token: 0x04000002 RID: 2
    public int damagePerSecond = 10;

    // Token: 0x04000003 RID: 3
    private float damageInterval = 1f;

    // Token: 0x04000004 RID: 4
    private bool playerInArea;

    // Token: 0x04000005 RID: 5
    private Coroutine damageCoroutine;
}

using System;
using UnityEngine;

// Token: 0x02000020 RID: 32
public class BasicEnemy : MonoBehaviour
{
    // Token: 0x06000085 RID: 133 RVA: 0x000037DD File Offset: 0x000019DD
    public void TakeDamage(int damage)
    {
        this.health -= damage;
        if (this.health <= 0)
        {
            this.DestroyEnemy();
        }
    }

    // Token: 0x06000086 RID: 134 RVA: 0x000037FC File Offset: 0x000019FC
    public void DestroyEnemy()
    {
        Object.Destroy(base.gameObject);
    }

    // Token: 0x04000070 RID: 112
    [Header("Stats")]
    public int health;
}

using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// Token: 0x02000038 RID: 56
public class BeastBehaviour : MonoBehaviour
{
    // Token: 0x0600012E RID: 302 RVA: 0x00006371 File Offset: 0x00004571
    private void Start()
    {
        this.animator = base.GetComponent<Animator>();
        this.agent = base.GetComponent<NavMeshAgent>();
        this.sphereCollider = base.GetComponent<SphereCollider>();
    }

    // Token: 0x0600012F RID: 303 RVA: 0x00006397 File Offset: 0x00004597
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.OnEnterArea();
        }
    }

    // Token: 0x06000130 RID: 304 RVA: 0x000063AC File Offset: 0x000045AC
    public void OnEnterArea()
    {
        if (this.firstTime)
        {
            this.sphereCollider.enabled = false;
            this.firstTime = false;
        }
        this.dialogues[this.dialogueNum].Invoke();
        if (this.dialogueNum == 1)
        {
            GameObject[] array = GameObject.FindGameObjectsWithTag("Trotil");
            for (int i = 0; i < array.Length; i++)
            {
                Object.Destroy(array[i]);
            }
        }
        this.isCheckingEnd = true;
    }

    // Token: 0x06000131 RID: 305 RVA: 0x00006417 File Offset: 0x00004617
    private void Update()
    {
        if (this.isCheckingEnd)
        {
            this.CheckDialogueEnd();
        }
    }

    // Token: 0x06000132 RID: 306 RVA: 0x00006428 File Offset: 0x00004628
    public void CheckDialogueEnd()
    {
        if (!this.UI.activeInHierarchy)
        {
            this.isCheckingEnd = false;
            if (this.dialogueNum == 0)
            {
                base.Invoke("StartFight", 0.8f);
            }
            if (this.dialogueNum == 1)
            {
                base.Invoke("DisappearCompletely", 0.8f);
            }
        }
    }

    // Token: 0x06000133 RID: 307 RVA: 0x0000647A File Offset: 0x0000467A
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.skip.Invoke();
        }
    }

    // Token: 0x06000134 RID: 308 RVA: 0x00006494 File Offset: 0x00004694
    private void StartFight()
    {
        this.pier1.presence = "true";
        this.pier2.presence = "true";
        this.pier1.SavePresence();
        this.pier2.SavePresence();
        this.animator.SetBool("hasIncreased", true);
        this.agent.speed = 3.3f;
        this.trotilSpawner.SetActive(true);
        this.afterLab.SetActive(true);
        this.barrier.SetActive(false);
        Object.FindObjectOfType<MusicManager>();
    }

    // Token: 0x06000135 RID: 309 RVA: 0x00006524 File Offset: 0x00004724
    public void OnBoatEntered()
    {
        this.agent.enabled = false;
        this.sphereCollider.enabled = true;
        this.trap.SetActive(true);
        this.dialogueNum = 1;
        this.pier1.presence = "false";
        this.pier1.SavePresence();
        base.Invoke("ActivateTip", 1.7f);
        this.animator.SetBool("hasIncreased", false);
        this.agent.speed = 0f;
        Object.Instantiate<GameObject>(this.smoke, base.transform.position, Quaternion.identity);
        base.transform.position = this.finalPos.position;
        base.transform.rotation = this.finalPos.rotation;
        this.agent.enabled = true;
    }

    // Token: 0x06000136 RID: 310 RVA: 0x000065FC File Offset: 0x000047FC
    public void ActivateTip()
    {
        if (this.tip != null)
        {
            this.tip.SetActive(true);
        }
    }

    // Token: 0x06000137 RID: 311 RVA: 0x00006618 File Offset: 0x00004818
    public void DisappearCompletely()
    {
        Object.FindObjectOfType<MusicManager>();
        Object.Instantiate<GameObject>(this.smoke, base.transform.position, Quaternion.identity);
        this.flowerPile.SetActive(true);
        this.trap.SetActive(false);
        this.pier2.presence = "false";
        this.pier2.SavePresence();
        Object.Destroy(base.gameObject);
    }

    // Token: 0x04000161 RID: 353
    public bool firstTime;

    // Token: 0x04000162 RID: 354
    public bool isCheckingEnd;

    // Token: 0x04000163 RID: 355
    public GameObject UI;

    // Token: 0x04000164 RID: 356
    public GameObject trotilSpawner;

    // Token: 0x04000165 RID: 357
    public UnityEvent[] dialogues;

    // Token: 0x04000166 RID: 358
    public UnityEvent skip;

    // Token: 0x04000167 RID: 359
    public GameObject afterLab;

    // Token: 0x04000168 RID: 360
    public GameObject barrier;

    // Token: 0x04000169 RID: 361
    public GameObject tip;

    // Token: 0x0400016A RID: 362
    public GameObject smoke;

    // Token: 0x0400016B RID: 363
    public GameObject trap;

    // Token: 0x0400016C RID: 364
    public Pier pier1;

    // Token: 0x0400016D RID: 365
    public Pier pier2;

    // Token: 0x0400016E RID: 366
    public GameObject flowerPile;

    // Token: 0x0400016F RID: 367
    public Transform finalPos;

    // Token: 0x04000170 RID: 368
    public int dialogueNum;

    // Token: 0x04000171 RID: 369
    private Animator animator;

    // Token: 0x04000172 RID: 370
    private NavMeshAgent agent;

    // Token: 0x04000173 RID: 371
    private SphereCollider sphereCollider;
}

using System;
using Ditzelgames;
using UnityEngine;

// Token: 0x02000039 RID: 57
public class BoatController : MonoBehaviour
{
    // Token: 0x06000139 RID: 313 RVA: 0x0000668D File Offset: 0x0000488D
    private void Start()
    {
        this.ParticleSystem = base.GetComponentInChildren<ParticleSystem>();
        this.rb = base.GetComponent<Rigidbody>();
        this.StartRotation = this.motor.localRotation;
    }

    // Token: 0x0600013A RID: 314 RVA: 0x000066B8 File Offset: 0x000048B8
    private void FixedUpdate()
    {
        this.MoveBoat();
    }

    // Token: 0x0600013B RID: 315 RVA: 0x000066C0 File Offset: 0x000048C0
    private void MoveBoat()
    {
        Vector3 forward = base.transform.forward;
        int num = 0;
        if (Input.GetKey(KeyCode.A))
        {
            num = -1;
        }
        if (Input.GetKey(KeyCode.D))
        {
            num = 1;
        }
        this.rb.AddForceAtPosition((float)num * base.transform.right * this.steerPower / 100f, this.motor.position);
        Vector3 a = Vector3.Scale(new Vector3(1f, 0f, 1f), this.orientation.forward);
        Vector3 zero = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
        {
            PhysicsHelper.ApplyForceToReachVelocity(this.rb, a * this.speed, this.power, ForceMode.Force);
        }
        if (Input.GetKey(KeyCode.S))
        {
            PhysicsHelper.ApplyForceToReachVelocity(this.rb, a * -this.speed, this.power, ForceMode.Force);
        }
        this.motor.SetPositionAndRotation(this.motor.position, base.transform.rotation * this.StartRotation * Quaternion.Euler(0f, 30f * (float)num, 0f));
        if (this.ParticleSystem != null)
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S))
            {
                this.ParticleSystem.Play();
            }
            else
            {
                this.ParticleSystem.Pause();
            }
        }
        bool flag = Vector3.Cross(this.orientation.forward, this.rb.velocity).y < 0f;
        this.rb.velocity = Quaternion.AngleAxis(Vector3.SignedAngle(this.rb.velocity, (flag ? 1f : 0f) * base.transform.forward, Vector3.up) * this.drag, Vector3.up) * this.rb.velocity;
    }

    // Token: 0x04000174 RID: 372
    public Transform motor;

    // Token: 0x04000175 RID: 373
    public float speed;

    // Token: 0x04000176 RID: 374
    public float drag;

    // Token: 0x04000177 RID: 375
    public float power;

    // Token: 0x04000178 RID: 376
    public float steerPower = 200f;

    // Token: 0x04000179 RID: 377
    public Transform orientation;

    // Token: 0x0400017A RID: 378
    private float horizontalInput;

    // Token: 0x0400017B RID: 379
    private float verticalInput;

    // Token: 0x0400017C RID: 380
    private Vector3 moveDirection;

    // Token: 0x0400017D RID: 381
    private Rigidbody rb;

    // Token: 0x0400017E RID: 382
    protected Quaternion StartRotation;

    // Token: 0x0400017F RID: 383
    protected ParticleSystem ParticleSystem;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x0200004E RID: 78
public class BoatSwitch : MonoBehaviour
{
    // Token: 0x0600019D RID: 413 RVA: 0x00008016 File Offset: 0x00006216
    private void Update()
    {
        if (this.pierIsNear && Input.GetKeyDown(KeyCode.LeftShift))
        {
            this.ChangeBoatState();
        }
    }

    // Token: 0x0600019E RID: 414 RVA: 0x00008034 File Offset: 0x00006234
    private void ChangeBoatState()
    {
        if (this.player.enabled)
        {
            this.player.gameObject.SetActive(false);
            this.boat.enabled = true;
            this.player.enabled = false;
            this.boatText.text = "Shift - покинуть лодку";
            this.player.transform.position = this.seatPos.position;
            this.player.transform.parent = base.transform;
            this.cam.currentMode = MoveCamera.UpdateMode.FixedUpdate;
            this.player.GetComponent<Rigidbody>().isKinematic = true;
            if (this.trotil != null)
            {
                this.trotil.enabled = true;
                BeastBehaviour beastBehaviour = Object.FindObjectOfType<BeastBehaviour>();
                beastBehaviour.trotilSpawner.SetActive(false);
                beastBehaviour.OnBoatEntered();
            }
            this.player.gameObject.SetActive(true);
            return;
        }
        this.player.GetComponent<Rigidbody>().isKinematic = false;
        this.boat.enabled = false;
        this.player.enabled = true;
        this.boatText.text = "Shift - сесть в лодку";
        this.player.transform.position = this.pierPos.position;
        if (this.trotil != null)
        {
            this.trotil.enabled = false;
            BeastBehaviour beastBehaviour2 = Object.FindObjectOfType<BeastBehaviour>();
            if (beastBehaviour2 != null)
            {
                beastBehaviour2.OnEnterArea();
                beastBehaviour2.pier2.presence = "false";
                beastBehaviour2.pier2.SavePresence();
            }
        }
        this.player.transform.parent = null;
        this.cam.currentMode = MoveCamera.UpdateMode.LateUpdate;
    }

    // Token: 0x040001F2 RID: 498
    public PlayerController player;

    // Token: 0x040001F3 RID: 499
    public BoatController boat;

    // Token: 0x040001F4 RID: 500
    public Transform seatPos;

    // Token: 0x040001F5 RID: 501
    public Transform pierPos;

    // Token: 0x040001F6 RID: 502
    public MoveCamera cam;

    // Token: 0x040001F7 RID: 503
    public CurveSpawner trotil;

    // Token: 0x040001F8 RID: 504
    public TextMeshProUGUI boatText;

    // Token: 0x040001F9 RID: 505
    [HideInInspector]
    public bool pierIsNear;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Token: 0x02000021 RID: 33
public class BooBoss : MonoBehaviour
{
    // Token: 0x1700000D RID: 13
    // (get) Token: 0x06000088 RID: 136 RVA: 0x00003811 File Offset: 0x00001A11
    // (set) Token: 0x06000089 RID: 137 RVA: 0x00003819 File Offset: 0x00001A19
    public bool hasCompletedCutscene { get; set; }

    // Token: 0x1700000E RID: 14
    // (get) Token: 0x0600008A RID: 138 RVA: 0x00003822 File Offset: 0x00001A22
    // (set) Token: 0x0600008B RID: 139 RVA: 0x0000382A File Offset: 0x00001A2A
    public bool hasSeenTip { get; set; }

    // Token: 0x0600008C RID: 140 RVA: 0x00003833 File Offset: 0x00001A33
    private void Awake()
    {
        this.agent = base.GetComponent<NavMeshAgent>();
        this.animator = base.GetComponentInChildren<Animator>();
        this.health = this.startHealth;
    }

    // Token: 0x0600008D RID: 141 RVA: 0x0000385C File Offset: 0x00001A5C
    private void OnEnable()
    {
        CurveSpawner curveSpawner = this.phantomSpawner;
        curveSpawner.hasDoneSpawning = (Action)Delegate.Combine(curveSpawner.hasDoneSpawning, new Action(this.FinishPhantoms));
        CurveSpawner curveSpawner2 = this.phantomHardSpawner;
        curveSpawner2.hasDoneSpawning = (Action)Delegate.Combine(curveSpawner2.hasDoneSpawning, new Action(this.FinishPhantoms));
    }

    // Token: 0x0600008E RID: 142 RVA: 0x000038B8 File Offset: 0x00001AB8
    private void OnDisable()
    {
        CurveSpawner curveSpawner = this.phantomSpawner;
        curveSpawner.hasDoneSpawning = (Action)Delegate.Remove(curveSpawner.hasDoneSpawning, new Action(this.FinishPhantoms));
        CurveSpawner curveSpawner2 = this.phantomHardSpawner;
        curveSpawner2.hasDoneSpawning = (Action)Delegate.Remove(curveSpawner2.hasDoneSpawning, new Action(this.FinishPhantoms));
    }

    // Token: 0x0600008F RID: 143 RVA: 0x00003913 File Offset: 0x00001B13
    private void Start()
    {
        if (this.hasCompletedCutscene)
        {
            this.EnableBoss();
            return;
        }
        this.cutscene.SetActive(true);
    }

    // Token: 0x06000090 RID: 144 RVA: 0x00003930 File Offset: 0x00001B30
    public void EnableBoss()
    {
        this.hasCompletedCutscene = true;
        this.cutsceneChecker.MeetBooBoss();
        this.healthSlider.gameObject.SetActive(true);
        this.damage = this.startDamage;
        this.agent.speed = this.startSpeed;
        this.isEnabled = true;
        MusicManager.instance.EnableMusic(5);
        base.GetComponent<SoundManager>().enabled = true;
    }

    // Token: 0x06000091 RID: 145 RVA: 0x0000399C File Offset: 0x00001B9C
    private void Update()
    {
        if (this.isEnabled)
        {
            this.healthSlider.value = this.health / this.startHealth;
            this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
            this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
            if (!this.playerInSightRange && !this.playerInAttackRange)
            {
                this.Patroling();
            }
            if (this.playerInSightRange && !this.playerInAttackRange)
            {
                this.ChasePlayer();
            }
            if (this.playerInAttackRange && this.playerInSightRange)
            {
                this.AttackPlayer();
            }
        }
    }

    // Token: 0x06000092 RID: 146 RVA: 0x00003A5C File Offset: 0x00001C5C
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x06000093 RID: 147 RVA: 0x00003AC0 File Offset: 0x00001CC0
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x06000094 RID: 148 RVA: 0x00003B66 File Offset: 0x00001D66
    private void ChasePlayer()
    {
        this.agent.SetDestination(this.player.position);
        if (!this.isAttacking)
        {
            this.isAttacking = true;
            this.ChooseMoveset();
        }
    }

    // Token: 0x06000095 RID: 149 RVA: 0x00003B94 File Offset: 0x00001D94
    private void AttackPlayer()
    {
        this.agent.SetDestination(base.transform.position);
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x06000096 RID: 150 RVA: 0x00003C30 File Offset: 0x00001E30
    public void ChooseMoveset()
    {
        if (this.health < 170f && !this.hasActivatedForceField)
        {
            this.ActivateForceField();
            return;
        }
        if (this.health < 120f && !this.hasSummonedPhantoms && !this.forceField.activeInHierarchy)
        {
            this.SummonPhantoms();
            return;
        }
        int num = Random.Range(0, 3);
        if (num == 0)
        {
            base.StartCoroutine(this.SpawnGroupOfBullets(1, 6f));
            return;
        }
        if (num != 1)
        {
            if (num == 2)
            {
                if (this.health < 120f && !this.forceField.activeInHierarchy && this.canSummonPhantoms)
                {
                    this.SummonPhantoms();
                }
                else
                {
                    base.StartCoroutine(this.SpawnCandles());
                }
                if (this.health < 70f)
                {
                    for (int i = 0; i < 7; i++)
                    {
                        base.StartCoroutine(this.SummonLightning());
                    }
                }
            }
            return;
        }
        if (this.health > 170f || !this.canActivateForceField)
        {
            base.StartCoroutine(this.SpawnCandles());
            return;
        }
        this.ActivateForceField();
    }

    // Token: 0x06000097 RID: 151 RVA: 0x00003D30 File Offset: 0x00001F30
    public IEnumerator SpawnGroupOfBullets(int totalbullets, float cooldown)
    {
        int numberOfBullets = 0;
        while (numberOfBullets <= totalbullets)
        {
            this.SpawnBullet(80f);
            int num = numberOfBullets;
            numberOfBullets = num + 1;
            yield return new WaitForSeconds(cooldown);
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x06000098 RID: 152 RVA: 0x00003D4D File Offset: 0x00001F4D
    public IEnumerator SpawnCandles()
    {
        if (this.health > 100f)
        {
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(0.5f);
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(2f);
        }
        else
        {
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(0.5f);
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(0.5f);
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(0.5f);
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x06000099 RID: 153 RVA: 0x00003D5C File Offset: 0x00001F5C
    public void SpawnCandlesPack()
    {
        this.SpawnCandle(new Vector3(-0.1f, 0f), -10f);
        this.SpawnCandle(new Vector3(0f, 0f), 0f);
        this.SpawnCandle(new Vector3(0.1f, 0f), 10f);
        if (this.health < 100f)
        {
            this.SpawnCandle(new Vector3(-0.2f, 0f), -20f);
            this.SpawnCandle(new Vector3(0.2f, 0f), 20f);
        }
    }

    // Token: 0x0600009A RID: 154 RVA: 0x00003DF8 File Offset: 0x00001FF8
    public void SpawnCandle(Vector3 positionOffset, float rotationOffset)
    {
        this.attackPos.LookAt(this.player);
        Quaternion rotation = Quaternion.Euler(this.attackPos.eulerAngles.x + 90f, this.attackPos.eulerAngles.y + rotationOffset, this.attackPos.eulerAngles.z);
        Object.Instantiate<GameObject>(this.candle, this.attackPos.position + positionOffset, rotation);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
    }

    // Token: 0x0600009B RID: 155 RVA: 0x00003E90 File Offset: 0x00002090
    public void SpawnBullet(float rotationOffset)
    {
        this.attackPos.LookAt(this.player);
        this.newBullet = Object.Instantiate<GameObject>(this.bullet, this.attackPos.position, this.attackPos.rotation);
        this.newBullet.transform.parent = this.attackPos;
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
    }

    // Token: 0x0600009C RID: 156 RVA: 0x00003F08 File Offset: 0x00002108
    public void ActivateForceField()
    {
        if (!this.hasSeenTip)
        {
            this.hasSeenTip = true;
            this.tipTextAnimator.SetTrigger("isTipping");
            this.cutsceneChecker.HasSeenTip();
        }
        this.hasActivatedForceField = true;
        this.canActivateForceField = false;
        this.forceField.SetActive(true);
        this.forceField.GetComponent<Animator>().SetBool("isActivated", true);
        this.candleController.LightCandles();
        this.isAttacking = false;
    }

    // Token: 0x0600009D RID: 157 RVA: 0x00003F81 File Offset: 0x00002181
    public IEnumerator DeactivateForceField()
    {
        this.forceField.GetComponent<Animator>().SetBool("isActivated", false);
        yield return new WaitForSeconds(0.5f);
        this.forceField.SetActive(false);
        yield return new WaitForSeconds(15f);
        this.canActivateForceField = true;
        yield break;
    }

    // Token: 0x0600009E RID: 158 RVA: 0x00003F90 File Offset: 0x00002190
    public void SummonPhantoms()
    {
        this.hasSummonedPhantoms = true;
        this.canSummonPhantoms = false;
        this.damage = 0;
        this.agent.speed = 0f;
        Object.Instantiate<GameObject>(this.phantomEffect, base.transform.position, Quaternion.identity);
        foreach (object obj in base.transform)
        {
            ((Transform)obj).gameObject.SetActive(false);
        }
        if (this.health > 70f)
        {
            this.phantomSpawner.enabled = true;
            return;
        }
        this.phantomHardSpawner.enabled = true;
    }

    // Token: 0x0600009F RID: 159 RVA: 0x00004054 File Offset: 0x00002254
    public void FinishPhantoms()
    {
        base.StartCoroutine(this.ReturnBoss());
    }

    // Token: 0x060000A0 RID: 160 RVA: 0x00004063 File Offset: 0x00002263
    public IEnumerator ReturnBoss()
    {
        this.damage = this.startDamage;
        this.agent.speed = this.startSpeed;
        this.canActivateForceField = false;
        foreach (object obj in base.transform)
        {
            ((Transform)obj).gameObject.SetActive(true);
        }
        this.isAttacking = false;
        yield return new WaitForSeconds(20f);
        this.canSummonPhantoms = true;
        this.canActivateForceField = true;
        yield break;
    }

    // Token: 0x060000A1 RID: 161 RVA: 0x00004072 File Offset: 0x00002272
    public IEnumerator SummonLightning()
    {
        Vector3 lightningPos = this.RandomPointInBox(new Vector3(18f, 0f, 18f));
        Object.Instantiate<GameObject>(this.preLightning, lightningPos, Quaternion.identity);
        yield return new WaitForSeconds(1f);
        Object.Instantiate<GameObject>(this.lightning, lightningPos, Quaternion.identity);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
        yield break;
    }

    // Token: 0x060000A2 RID: 162 RVA: 0x00004084 File Offset: 0x00002284
    private Vector3 RandomPointInBox(Vector3 size)
    {
        return new Vector3(this.player.position.x, 0f, this.player.position.z) + new Vector3((Random.value - 0.5f) * size.x, (Random.value - 0.5f) * size.y, (Random.value - 0.5f) * size.z);
    }

    // Token: 0x060000A3 RID: 163 RVA: 0x000040FB File Offset: 0x000022FB
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x060000A4 RID: 164 RVA: 0x00004104 File Offset: 0x00002304
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x060000A5 RID: 165 RVA: 0x00004134 File Offset: 0x00002334
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.finalMushroom, base.transform.position, Quaternion.identity);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x060000A6 RID: 166 RVA: 0x00004190 File Offset: 0x00002390
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x04000071 RID: 113
    public LayerMask whatIsGround;

    // Token: 0x04000072 RID: 114
    public LayerMask whatIsPlayer;

    // Token: 0x04000073 RID: 115
    public Vector3 walkPoint;

    // Token: 0x04000074 RID: 116
    private bool walkPointSet;

    // Token: 0x04000075 RID: 117
    public float walkPointRange;

    // Token: 0x04000076 RID: 118
    public float timeBetweenAttacks;

    // Token: 0x04000077 RID: 119
    private bool isAttacking;

    // Token: 0x04000078 RID: 120
    private bool alreadyAttacked;

    // Token: 0x04000079 RID: 121
    public float sightRange;

    // Token: 0x0400007A RID: 122
    public float attackRange;

    // Token: 0x0400007B RID: 123
    public bool playerInSightRange;

    // Token: 0x0400007C RID: 124
    public bool playerInAttackRange;

    // Token: 0x0400007F RID: 127
    public float startHealth;

    // Token: 0x04000080 RID: 128
    public float health;

    // Token: 0x04000081 RID: 129
    public int startDamage;

    // Token: 0x04000082 RID: 130
    public float startSpeed;

    // Token: 0x04000083 RID: 131
    private int damage;

    // Token: 0x04000084 RID: 132
    private int restCount;

    // Token: 0x04000085 RID: 133
    private bool canActivateForceField = true;

    // Token: 0x04000086 RID: 134
    private bool canSummonPhantoms = true;

    // Token: 0x04000087 RID: 135
    private bool hasActivatedForceField;

    // Token: 0x04000088 RID: 136
    private bool hasSummonedPhantoms;

    // Token: 0x04000089 RID: 137
    private bool isEnabled;

    // Token: 0x0400008A RID: 138
    public GameObject cutscene;

    // Token: 0x0400008B RID: 139
    public GameObject bullet;

    // Token: 0x0400008C RID: 140
    public GameObject candle;

    // Token: 0x0400008D RID: 141
    public GameObject forceField;

    // Token: 0x0400008E RID: 142
    public GameObject preLightning;

    // Token: 0x0400008F RID: 143
    public GameObject lightning;

    // Token: 0x04000090 RID: 144
    public Transform attackPos;

    // Token: 0x04000091 RID: 145
    public Transform lightningCenter;

    // Token: 0x04000092 RID: 146
    public GameObject magicSound;

    // Token: 0x04000093 RID: 147
    public GameObject phantomEffect;

    // Token: 0x04000094 RID: 148
    public GameObject deathEffect;

    // Token: 0x04000095 RID: 149
    public GameObject finalMushroom;

    // Token: 0x04000096 RID: 150
    public CandleController candleController;

    // Token: 0x04000097 RID: 151
    public CurveSpawner phantomSpawner;

    // Token: 0x04000098 RID: 152
    public CurveSpawner phantomHardSpawner;

    // Token: 0x04000099 RID: 153
    public CutsceneChecker cutsceneChecker;

    // Token: 0x0400009A RID: 154
    public Animator tipTextAnimator;

    // Token: 0x0400009B RID: 155
    public Slider healthSlider;

    // Token: 0x0400009C RID: 156
    private NavMeshAgent agent;

    // Token: 0x0400009D RID: 157
    public Transform player;

    // Token: 0x0400009E RID: 158
    public Transform playerLookPoint;

    // Token: 0x0400009F RID: 159
    private Animator animator;

    // Token: 0x040000A0 RID: 160
    private GameObject newBullet;

    // Token: 0x040000A1 RID: 161
    private Vector3 stickDirection;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x02000003 RID: 3
public class BooTrigger : MonoBehaviour
{
    // Token: 0x06000005 RID: 5 RVA: 0x00002138 File Offset: 0x00000338
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.border.SetActive(false);
            this.doorsAnimator.SetTrigger("isOpened");
            this.navMeshAgent.speed = 8f;
            Object.Instantiate<GameObject>(this.sound, other.transform.position, Quaternion.identity).transform.parent = other.transform;
            MusicManager.instance.EnableMusic(1);
            base.GetComponent<Collider>().enabled = false;
        }
    }

    // Token: 0x04000006 RID: 6
    [SerializeField]
    private Animator doorsAnimator;

    // Token: 0x04000007 RID: 7
    [SerializeField]
    private NavMeshAgent navMeshAgent;

    // Token: 0x04000008 RID: 8
    [SerializeField]
    private GameObject sound;

    // Token: 0x04000009 RID: 9
    [SerializeField]
    private GameObject border;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Token: 0x02000022 RID: 34
public class Boss : MonoBehaviour
{
    // Token: 0x060000A8 RID: 168 RVA: 0x000041F4 File Offset: 0x000023F4
    private void Awake()
    {
        this.player = GameObject.FindGameObjectWithTag("Player").transform;
        this.agent = base.GetComponent<NavMeshAgent>();
        this.health = this.startHealth;
        this.healthSlider = Object.FindObjectOfType<MushroomGame>().bossHP.GetComponent<Slider>();
    }

    // Token: 0x060000A9 RID: 169 RVA: 0x00004243 File Offset: 0x00002443
    private void Start()
    {
        this.healthSlider.gameObject.SetActive(true);
    }

    // Token: 0x060000AA RID: 170 RVA: 0x00004258 File Offset: 0x00002458
    private void Update()
    {
        this.healthSlider.value = this.health / this.startHealth;
        this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
        if (!this.playerInSightRange && !this.playerInAttackRange)
        {
            this.Patroling();
        }
        if (this.playerInSightRange && !this.playerInAttackRange)
        {
            this.ChasePlayer();
        }
        if (this.playerInAttackRange && this.playerInSightRange)
        {
            this.AttackPlayer();
        }
    }

    // Token: 0x060000AB RID: 171 RVA: 0x00004310 File Offset: 0x00002510
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x060000AC RID: 172 RVA: 0x00004374 File Offset: 0x00002574
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x060000AD RID: 173 RVA: 0x0000441A File Offset: 0x0000261A
    private void ChasePlayer()
    {
        this.agent.SetDestination(this.player.position);
        if (!this.isAttacking)
        {
            this.isAttacking = true;
            this.ChooseMoveset();
        }
    }

    // Token: 0x060000AE RID: 174 RVA: 0x00004448 File Offset: 0x00002648
    private void AttackPlayer()
    {
        this.agent.SetDestination(base.transform.position);
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x060000AF RID: 175 RVA: 0x000044E4 File Offset: 0x000026E4
    public void ChooseMoveset()
    {
        int num = Random.Range(0, 3);
        if (num == 0)
        {
            base.StartCoroutine(this.SpawnGroupSticks(false, 5, 0.3f));
            return;
        }
        if (num != 1)
        {
            if (num == 2)
            {
                for (int i = 0; i < 7; i++)
                {
                    base.StartCoroutine(this.SummonLightning());
                }
            }
            return;
        }
        if (this.health > 15f)
        {
            base.StartCoroutine(this.SpawnGroupSticks(true, 3, 0.6f));
            return;
        }
        base.StartCoroutine(this.InstantiateEarthquake());
    }

    // Token: 0x060000B0 RID: 176 RVA: 0x00004562 File Offset: 0x00002762
    public IEnumerator SpawnGroupSticks(bool isNormal, int totalSticks, float cooldown)
    {
        int numberOfSticks = 0;
        while (numberOfSticks <= totalSticks)
        {
            this.SpawnStick(isNormal);
            int num = numberOfSticks;
            numberOfSticks = num + 1;
            yield return new WaitForSeconds(cooldown);
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x060000B1 RID: 177 RVA: 0x00004588 File Offset: 0x00002788
    public void SpawnStick(bool isNormal)
    {
        if (isNormal)
        {
            Object.Instantiate<GameObject>(this.stick, this.attackPosNormal.position, Quaternion.Euler(0f, (float)Random.Range(0, 360), (float)Random.Range(0, 360)));
        }
        else
        {
            Object.Instantiate<GameObject>(this.stick, this.attackPosDown.position, this.attackPosDown.rotation);
        }
        Object.Instantiate<GameObject>(this.magicSound, this.attackPosNormal.position, Quaternion.identity);
    }

    // Token: 0x060000B2 RID: 178 RVA: 0x00004611 File Offset: 0x00002811
    public IEnumerator SummonLightning()
    {
        Vector3 lightningPos = this.RandomPointInBox(new Vector3(15f, 0f, 15f));
        Object.Instantiate<GameObject>(this.preLightning, lightningPos, Quaternion.identity);
        yield return new WaitForSeconds(1f);
        Object.Instantiate<GameObject>(this.lightning, lightningPos, Quaternion.identity);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPosNormal.position, Quaternion.identity);
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x060000B3 RID: 179 RVA: 0x00004620 File Offset: 0x00002820
    public IEnumerator InstantiateEarthquake()
    {
        Object.Instantiate<GameObject>(this.earthquake, this.lightningCenter.position, this.lightningCenter.rotation);
        yield return new WaitForSeconds(3f);
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x060000B4 RID: 180 RVA: 0x00004630 File Offset: 0x00002830
    private Vector3 RandomPointInBox(Vector3 size)
    {
        return new Vector3(this.player.position.x, 0f, this.player.position.z) + new Vector3((Random.value - 0.5f) * size.x, (Random.value - 0.5f) * size.y, (Random.value - 0.5f) * size.z);
    }

    // Token: 0x060000B5 RID: 181 RVA: 0x000046A7 File Offset: 0x000028A7
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x060000B6 RID: 182 RVA: 0x000046B0 File Offset: 0x000028B0
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x060000B7 RID: 183 RVA: 0x000046E0 File Offset: 0x000028E0
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.finalMushroom, base.transform.position, Quaternion.identity);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x060000B8 RID: 184 RVA: 0x0000473C File Offset: 0x0000293C
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x040000A2 RID: 162
    public LayerMask whatIsGround;

    // Token: 0x040000A3 RID: 163
    public LayerMask whatIsPlayer;

    // Token: 0x040000A4 RID: 164
    public Vector3 walkPoint;

    // Token: 0x040000A5 RID: 165
    private bool walkPointSet;

    // Token: 0x040000A6 RID: 166
    public float walkPointRange;

    // Token: 0x040000A7 RID: 167
    public float timeBetweenAttacks;

    // Token: 0x040000A8 RID: 168
    private bool isAttacking;

    // Token: 0x040000A9 RID: 169
    private bool alreadyAttacked;

    // Token: 0x040000AA RID: 170
    public float sightRange;

    // Token: 0x040000AB RID: 171
    public float attackRange;

    // Token: 0x040000AC RID: 172
    public bool playerInSightRange;

    // Token: 0x040000AD RID: 173
    public bool playerInAttackRange;

    // Token: 0x040000AE RID: 174
    public float startHealth;

    // Token: 0x040000AF RID: 175
    private float health;

    // Token: 0x040000B0 RID: 176
    public int damage;

    // Token: 0x040000B1 RID: 177
    public GameObject stick;

    // Token: 0x040000B2 RID: 178
    public GameObject preLightning;

    // Token: 0x040000B3 RID: 179
    public GameObject lightning;

    // Token: 0x040000B4 RID: 180
    public GameObject earthquake;

    // Token: 0x040000B5 RID: 181
    public Transform attackPosNormal;

    // Token: 0x040000B6 RID: 182
    public Transform attackPosDown;

    // Token: 0x040000B7 RID: 183
    public Transform lightningCenter;

    // Token: 0x040000B8 RID: 184
    public GameObject magicSound;

    // Token: 0x040000B9 RID: 185
    public GameObject deathEffect;

    // Token: 0x040000BA RID: 186
    public GameObject finalMushroom;

    // Token: 0x040000BB RID: 187
    public Slider healthSlider;

    // Token: 0x040000BC RID: 188
    private NavMeshAgent agent;

    // Token: 0x040000BD RID: 189
    private Transform player;

    // Token: 0x040000BE RID: 190
    private GameObject newStick;

    // Token: 0x040000BF RID: 191
    private Vector3 stickDirection;
}

using System;
using UnityEngine;

// Token: 0x02000023 RID: 35
public class BossMovesets : MonoBehaviour
{
    // Token: 0x060000BA RID: 186 RVA: 0x00004791 File Offset: 0x00002991
    private void Start()
    {
    }

    // Token: 0x060000BB RID: 187 RVA: 0x00004793 File Offset: 0x00002993
    private void Update()
    {
    }
}

using System;
using UnityEngine;

// Token: 0x0200001C RID: 28
public class BossMusicSwitcher : MonoBehaviour
{
    // Token: 0x06000077 RID: 119 RVA: 0x00003552 File Offset: 0x00001752
    private void Start()
    {
        this.music = Object.FindObjectOfType<MusicManager>();
    }

    // Token: 0x06000078 RID: 120 RVA: 0x00003560 File Offset: 0x00001760
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (this.music.audioSource.clip != this.music.musicVariants[5] || this.music.audioSource.clip != this.music.musicVariants[6])
            {
                if (Random.Range(0, 10) == 9)
                {
                    this.music.EnableMusic(6);
                    return;
                }
                this.music.EnableMusic(5);
                return;
            }
            else
            {
                this.music.EnableMusic(0);
            }
        }
    }

    // Token: 0x04000063 RID: 99
    private MusicManager music;
}

using System;
using UnityEngine;

// Token: 0x02000004 RID: 4
public class BossTeleport : MonoBehaviour
{
    // Token: 0x06000007 RID: 7 RVA: 0x000021C8 File Offset: 0x000003C8
    private void OnEnable()
    {
        ItemCollector itemCollector = this.itemCollector;
        itemCollector.hasCollected = (Action)Delegate.Combine(itemCollector.hasCollected, new Action(this.CheckFlags));
    }

    // Token: 0x06000008 RID: 8 RVA: 0x000021F1 File Offset: 0x000003F1
    private void OnDisable()
    {
        ItemCollector itemCollector = this.itemCollector;
        itemCollector.hasCollected = (Action)Delegate.Remove(itemCollector.hasCollected, new Action(this.CheckFlags));
    }

    // Token: 0x06000009 RID: 9 RVA: 0x0000221A File Offset: 0x0000041A
    private void Start()
    {
        this.CheckFlags();
    }

    // Token: 0x0600000A RID: 10 RVA: 0x00002222 File Offset: 0x00000422
    private void CheckFlags()
    {
        if (this.itemCollector.items >= this.flagsCount)
        {
            this.EnablePortal();
        }
    }

    // Token: 0x0600000B RID: 11 RVA: 0x0000223D File Offset: 0x0000043D
    private void EnablePortal()
    {
        base.GetComponent<BoxCollider>().enabled = true;
        base.transform.GetChild(0).gameObject.SetActive(true);
    }

    // Token: 0x0600000C RID: 12 RVA: 0x00002264 File Offset: 0x00000464
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.root.position = this.teleportPoint.position;
            GravityController.instance.SetEarthGravity();
            base.Invoke("ActivateBoss", 0.5f);
        }
    }

    // Token: 0x0600000D RID: 13 RVA: 0x000022B3 File Offset: 0x000004B3
    public void ActivateBoss()
    {
        Debug.Log("ACTIVATIN");
        this.boss.SetActive(true);
    }

    // Token: 0x0400000A RID: 10
    [SerializeField]
    private GameObject boss;

    // Token: 0x0400000B RID: 11
    [SerializeField]
    private Transform teleportPoint;

    // Token: 0x0400000C RID: 12
    [SerializeField]
    private ItemCollector itemCollector;

    // Token: 0x0400000D RID: 13
    [SerializeField]
    private int flagsCount;
}

using System;
using UnityEngine;

// Token: 0x0200004F RID: 79
public class BuoyancyObject : MonoBehaviour
{
    // Token: 0x060001A0 RID: 416 RVA: 0x000081E0 File Offset: 0x000063E0
    private void FixedUpdate()
    {
        this.FloatersUnderWater = 0;
        for (int i = 0; i < this.Floaters.Length; i++)
        {
            float num = this.Floaters[i].position.y - this.WaterHeight;
            if (num < 0f)
            {
                this.Rb.AddForceAtPosition(Vector3.up * this.FloatingPower * Mathf.Abs(num), this.Floaters[i].position, ForceMode.Force);
                this.FloatersUnderWater++;
                if (!this.Underwater)
                {
                    this.Underwater = true;
                    this.SwitchState(true);
                }
            }
        }
        if (this.Underwater && this.FloatersUnderWater == 0)
        {
            this.Underwater = false;
            this.SwitchState(false);
        }
    }

    // Token: 0x060001A1 RID: 417 RVA: 0x000082A4 File Offset: 0x000064A4
    private void SwitchState(bool isUnderwater)
    {
        if (isUnderwater)
        {
            this.Rb.drag = this.UnderWaterDrag;
            this.Rb.angularDrag = this.UnderWaterAngularDrag;
            return;
        }
        this.Rb.drag = this.AirDrag;
        this.Rb.angularDrag = this.AirAngularDrag;
    }

    // Token: 0x040001FA RID: 506
    public Transform[] Floaters;

    // Token: 0x040001FB RID: 507
    public float UnderWaterDrag = 3f;

    // Token: 0x040001FC RID: 508
    public float UnderWaterAngularDrag = 1f;

    // Token: 0x040001FD RID: 509
    public float AirDrag;

    // Token: 0x040001FE RID: 510
    public float AirAngularDrag = 0.05f;

    // Token: 0x040001FF RID: 511
    public float FloatingPower = 15f;

    // Token: 0x04000200 RID: 512
    public float WaterHeight;

    // Token: 0x04000201 RID: 513
    public Rigidbody Rb;

    // Token: 0x04000202 RID: 514
    private bool Underwater;

    // Token: 0x04000203 RID: 515
    private int FloatersUnderWater;
}

using System;
using System.Collections;
using UnityEngine;

// Token: 0x0200003A RID: 58
public class BurgerBehaviour : MonoBehaviour
{
    // Token: 0x0600013D RID: 317 RVA: 0x000068C3 File Offset: 0x00004AC3
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.burgerUsual.SetActive(false);
            this.burgerUnusual.SetActive(true);
        }
    }

    // Token: 0x0600013E RID: 318 RVA: 0x000068EA File Offset: 0x00004AEA
    public IEnumerator OnBurgerDestroyed()
    {
        this.spawner.enabled = true;
        yield return new WaitForSeconds(15.5f);
        this.spawner.enabled = false;
        this.lockUI.OnFoodKilled();
        yield break;
    }

    // Token: 0x04000180 RID: 384
    public GameObject burgerUsual;

    // Token: 0x04000181 RID: 385
    public GameObject burgerUnusual;

    // Token: 0x04000182 RID: 386
    public CurveSpawner spawner;

    // Token: 0x04000183 RID: 387
    public LockUI lockUI;
}

using System;
using UnityEngine;

// Token: 0x02000005 RID: 5
public class Candle : MonoBehaviour
{
    // Token: 0x0600000F RID: 15 RVA: 0x000022D4 File Offset: 0x000004D4
    public void BlowOutCandle()
    {
        if (this.isLit)
        {
            this.isLit = false;
            Object.Instantiate<GameObject>(this.bang, this.bangPoint.position, Quaternion.identity);
            Action action = this.isBlown;
            if (action != null)
            {
                action();
            }
            this.LightCandle(false);
        }
    }

    // Token: 0x06000010 RID: 16 RVA: 0x00002324 File Offset: 0x00000524
    public void LightCandle(bool state)
    {
        base.transform.GetChild(0).gameObject.SetActive(state);
        this.isLit = state;
    }

    // Token: 0x0400000E RID: 14
    public bool isLit;

    // Token: 0x0400000F RID: 15
    public Action isBlown;

    // Token: 0x04000010 RID: 16
    [SerializeField]
    private Transform bangPoint;

    // Token: 0x04000011 RID: 17
    [SerializeField]
    private GameObject bang;
}

using System;
using UnityEngine;

// Token: 0x02000045 RID: 69
public class CandleBullet : MonoBehaviour
{
    // Token: 0x0600016C RID: 364 RVA: 0x00007278 File Offset: 0x00005478
    private void Update()
    {
        base.transform.Translate(Vector3.up * this.speed * Time.deltaTime);
    }

    // Token: 0x040001B2 RID: 434
    [SerializeField]
    private float speed;
}

using System;
using UnityEngine;

// Token: 0x02000006 RID: 6
public class CandleController : MonoBehaviour
{
    // Token: 0x06000012 RID: 18 RVA: 0x0000234C File Offset: 0x0000054C
    private void OnEnable()
    {
        foreach (Candle candle in this.candles)
        {
            candle.isBlown = (Action)Delegate.Combine(candle.isBlown, new Action(this.CheckCandles));
        }
    }

    // Token: 0x06000013 RID: 19 RVA: 0x00002394 File Offset: 0x00000594
    private void OnDisable()
    {
        foreach (Candle candle in this.candles)
        {
            candle.isBlown = (Action)Delegate.Remove(candle.isBlown, new Action(this.CheckCandles));
        }
    }

    // Token: 0x06000014 RID: 20 RVA: 0x000023DC File Offset: 0x000005DC
    public void LightCandles()
    {
        Candle[] array = this.candles;
        for (int i = 0; i < array.Length; i++)
        {
            array[i].LightCandle(true);
        }
    }

    // Token: 0x06000015 RID: 21 RVA: 0x00002408 File Offset: 0x00000608
    public void CheckCandles()
    {
        this.boss.TakeDamage(2);
        Candle[] array = this.candles;
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i].isLit)
            {
                return;
            }
        }
        base.StartCoroutine(this.boss.DeactivateForceField());
    }

    // Token: 0x04000012 RID: 18
    [SerializeField]
    private Candle[] candles;

    // Token: 0x04000013 RID: 19
    [SerializeField]
    private BooBoss boss;
}

using System;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x0200004C RID: 76
public class Card : MonoBehaviour
{
    // Token: 0x06000186 RID: 390 RVA: 0x000079E6 File Offset: 0x00005BE6
    private void Start()
    {
        this.animator = base.GetComponent<Animator>();
        this.cardImage = base.GetComponent<Image>();
        this.cardManager = Object.FindObjectOfType<CardManager>();
        this.cardImage.sprite = this.backSprite;
    }

    // Token: 0x06000187 RID: 391 RVA: 0x00007A1C File Offset: 0x00005C1C
    public void OnClick()
    {
        if (!this.cardManager.CanFlipCard(this))
        {
            return;
        }
        this.animator.SetBool("isFaceUp", true);
        this.animator.SetTrigger("isTriggered");
        this.cardManager.CardRevealed(this);
    }

    // Token: 0x06000188 RID: 392 RVA: 0x00007A5A File Offset: 0x00005C5A
    public void RevealFace()
    {
        this.cardImage.sprite = this.faceSprite;
    }

    // Token: 0x06000189 RID: 393 RVA: 0x00007A6D File Offset: 0x00005C6D
    public void Hide()
    {
        this.animator.SetBool("isFaceUp", false);
        this.animator.SetTrigger("isTriggered");
    }

    // Token: 0x0600018A RID: 394 RVA: 0x00007A90 File Offset: 0x00005C90
    public void RevealBack()
    {
        this.cardImage.sprite = this.backSprite;
    }

    // Token: 0x0600018B RID: 395 RVA: 0x00007AA3 File Offset: 0x00005CA3
    public void BooAnimation()
    {
        this.animator.SetTrigger("screamer");
    }

    // Token: 0x040001D7 RID: 471
    private Animator animator;

    // Token: 0x040001D8 RID: 472
    public Sprite faceSprite;

    // Token: 0x040001D9 RID: 473
    public Sprite backSprite;

    // Token: 0x040001DA RID: 474
    private Image cardImage;

    // Token: 0x040001DB RID: 475
    private CardManager cardManager;
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x0200004D RID: 77
public class CardManager : MonoBehaviour
{
    // Token: 0x0600018D RID: 397 RVA: 0x00007ABD File Offset: 0x00005CBD
    private void Start()
    {
    }

    // Token: 0x0600018E RID: 398 RVA: 0x00007ABF File Offset: 0x00005CBF
    private void OnEnable()
    {
        this.DeactivatePlayer();
    }

    // Token: 0x0600018F RID: 399 RVA: 0x00007AC7 File Offset: 0x00005CC7
    private void OnDisable()
    {
        this.ActivatePlayer();
    }

    // Token: 0x06000190 RID: 400 RVA: 0x00007AD0 File Offset: 0x00005CD0
    public void StartGame(List<Sprite> cardFaces)
    {
        Vector2 vector = new Vector2((float)(-(float)this.gridCols) / 2f, (float)this.gridRows / 2f);
        List<Sprite> list = new List<Sprite>(cardFaces);
        list.AddRange(cardFaces);
        this.Shuffle(list);
        for (int i = 0; i < this.gridRows; i++)
        {
            for (int j = 0; j < this.gridCols; j++)
            {
                GameObject gameObject = Object.Instantiate<GameObject>(this.cardPrefab, this.cardParent);
                Card component = gameObject.GetComponent<Card>();
                int index = i * this.gridCols + j;
                component.faceSprite = list[index];
                RectTransform component2 = gameObject.GetComponent<RectTransform>();
                float x = vector.x + (float)j * (this.cellWidth + this.offsetX);
                float y = vector.y - (float)i * (this.cellHeight + this.offsetY);
                component2.anchoredPosition = new Vector2(x, y);
            }
        }
    }

    // Token: 0x06000191 RID: 401 RVA: 0x00007BB4 File Offset: 0x00005DB4
    public void CardRevealed(Card card)
    {
        if (this.firstRevealed == null)
        {
            this.firstRevealed = card;
            if (card.faceSprite == this.booSprite && !this._hasSeenBoo)
            {
                this._hasSeenBoo = true;
                Object.Instantiate<GameObject>(this.screamerSound, this.player.transform.position, Quaternion.identity);
                Object.Instantiate<GameObject>(this.booSound, this.player.transform.position, Quaternion.identity);
                card.BooAnimation();
                return;
            }
        }
        else
        {
            this.secondRevealed = card;
            if (card.faceSprite == this.booSprite && !this._hasSeenBoo)
            {
                this._hasSeenBoo = true;
                Object.Instantiate<GameObject>(this.screamerSound, this.player.transform.position, Quaternion.identity);
                Object.Instantiate<GameObject>(this.booSound, this.player.transform.position, Quaternion.identity);
                card.BooAnimation();
            }
            base.StartCoroutine(this.CheckMatch());
        }
    }

    // Token: 0x06000192 RID: 402 RVA: 0x00007CC5 File Offset: 0x00005EC5
    private IEnumerator CheckMatch()
    {
        if (this.firstRevealed.faceSprite == this.secondRevealed.faceSprite)
        {
            yield return new WaitForSeconds(0.5f);
            this.MakeCardInvisible(this.firstRevealed);
            this.MakeCardInvisible(this.secondRevealed);
        }
        else
        {
            yield return new WaitForSeconds(1f);
            this.firstRevealed.Hide();
            this.secondRevealed.Hide();
        }
        this.firstRevealed = null;
        this.secondRevealed = null;
        this.CheckLevelComplete();
        yield break;
    }

    // Token: 0x06000193 RID: 403 RVA: 0x00007CD4 File Offset: 0x00005ED4
    private void MakeCardInvisible(Card card)
    {
        Image component = card.GetComponent<Image>();
        if (component != null)
        {
            component.color = new Color(1f, 1f, 1f, 0f);
        }
        card.GetComponent<Button>().interactable = false;
    }

    // Token: 0x06000194 RID: 404 RVA: 0x00007D1C File Offset: 0x00005F1C
    private void CheckLevelComplete()
    {
        using (IEnumerator enumerator = this.cardParent.GetEnumerator())
        {
            while (enumerator.MoveNext())
            {
                if (((Transform)enumerator.Current).GetComponent<Image>().color.a > 0f)
                {
                    return;
                }
            }
        }
        if (this.levelToLoad < 3)
        {
            this.levelToLoad++;
            this.LoadLevel(this.levelToLoad);
        }
        else
        {
            this.memoryGameTrigger.OnGameOver();
        }
        Debug.Log("Level Complete!");
    }

    // Token: 0x06000195 RID: 405 RVA: 0x00007DC0 File Offset: 0x00005FC0
    public bool CanFlipCard(Card card)
    {
        return card != this.firstRevealed && this.secondRevealed == null;
    }

    // Token: 0x06000196 RID: 406 RVA: 0x00007DE0 File Offset: 0x00005FE0
    private void Shuffle(List<Sprite> deck)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            Sprite value = deck[i];
            int index = Random.Range(i, deck.Count);
            deck[i] = deck[index];
            deck[index] = value;
        }
    }

    // Token: 0x06000197 RID: 407 RVA: 0x00007E2C File Offset: 0x0000602C
    private void ClearPreviousCards()
    {
        foreach (object obj in this.cardParent)
        {
            Object.Destroy(((Transform)obj).gameObject);
        }
    }

    // Token: 0x06000198 RID: 408 RVA: 0x00007E88 File Offset: 0x00006088
    public void LoadLevel(int level)
    {
        this.ClearPreviousCards();
        switch (level)
        {
            case 1:
                this.gridRows = 2;
                this.gridCols = 4;
                this.gridLayoutManager.ConfigureGrid(this.gridRows, this.gridCols, 150f, 150f, 10f, 10f);
                this.StartGame(this.cardFacesLevel1);
                return;
            case 2:
                this.gridRows = 3;
                this.gridCols = 4;
                this.gridLayoutManager.ConfigureGrid(this.gridRows, this.gridCols, 150f, 150f, 10f, 10f);
                this.StartGame(this.cardFacesLevel2);
                return;
            case 3:
                this.gridRows = 4;
                this.gridCols = 4;
                this.gridLayoutManager.ConfigureGrid(this.gridRows, this.gridCols, 150f, 150f, 10f, 10f);
                this.StartGame(this.cardFacesLevel3);
                return;
            default:
                return;
        }
    }

    // Token: 0x06000199 RID: 409 RVA: 0x00007F81 File Offset: 0x00006181
    public void Restart()
    {
        this.LoadLevel(this.levelToLoad);
    }

    // Token: 0x0600019A RID: 410 RVA: 0x00007F8F File Offset: 0x0000618F
    public void ActivatePlayer()
    {
        this.gun.SetActive(true);
        this.player.SetupDialogue(false);
        base.gameObject.SetActive(false);
    }

    // Token: 0x0600019B RID: 411 RVA: 0x00007FB5 File Offset: 0x000061B5
    public void DeactivatePlayer()
    {
        this.gun.SetActive(false);
        this.player.SetupDialogue(true);
        this.LoadLevel(this.levelToLoad);
    }

    // Token: 0x040001DC RID: 476
    public List<Sprite> cardFacesLevel1;

    // Token: 0x040001DD RID: 477
    public List<Sprite> cardFacesLevel2;

    // Token: 0x040001DE RID: 478
    public List<Sprite> cardFacesLevel3;

    // Token: 0x040001DF RID: 479
    public GameObject cardPrefab;

    // Token: 0x040001E0 RID: 480
    public Transform cardParent;

    // Token: 0x040001E1 RID: 481
    public GameObject gun;

    // Token: 0x040001E2 RID: 482
    public Sprite booSprite;

    // Token: 0x040001E3 RID: 483
    public PlayerController player;

    // Token: 0x040001E4 RID: 484
    public GridLayoutManager gridLayoutManager;

    // Token: 0x040001E5 RID: 485
    public MemoryGameTrigger memoryGameTrigger;

    // Token: 0x040001E6 RID: 486
    public GameObject screamerSound;

    // Token: 0x040001E7 RID: 487
    public GameObject booSound;

    // Token: 0x040001E8 RID: 488
    private Card firstRevealed;

    // Token: 0x040001E9 RID: 489
    private Card secondRevealed;

    // Token: 0x040001EA RID: 490
    public float cellWidth = 150f;

    // Token: 0x040001EB RID: 491
    public float cellHeight = 150f;

    // Token: 0x040001EC RID: 492
    public float offsetX = 10f;

    // Token: 0x040001ED RID: 493
    public float offsetY = 10f;

    // Token: 0x040001EE RID: 494
    public int gridRows;

    // Token: 0x040001EF RID: 495
    public int gridCols;

    // Token: 0x040001F0 RID: 496
    private int levelToLoad = 1;

    // Token: 0x040001F1 RID: 497
    private bool _hasSeenBoo;
}

using System;
using UnityEngine;

// Token: 0x02000059 RID: 89
public class CaveSoundDetector : MonoBehaviour
{
    // Token: 0x060001CC RID: 460 RVA: 0x00008DAE File Offset: 0x00006FAE
    private void Start()
    {
        this.music = Object.FindObjectOfType<MusicManager>();
    }

    // Token: 0x060001CD RID: 461 RVA: 0x00008DBB File Offset: 0x00006FBB
    private void OnTriggerEnter(Collider other)
    {
        other.CompareTag("Player");
    }

    // Token: 0x0400023E RID: 574
    private MusicManager music;
}

using System;
using UnityEngine;

// Token: 0x02000007 RID: 7
public class ChaseOver : MonoBehaviour
{
    // Token: 0x06000017 RID: 23 RVA: 0x0000245C File Offset: 0x0000065C
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.enemy.TakeDamage(10000);
            this.border.SetActive(true);
            this.player.moveSpeed = 3.5f;
            MusicManager.instance.EnableMusic(0);
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x04000014 RID: 20
    [SerializeField]
    private Enemy enemy;

    // Token: 0x04000015 RID: 21
    [SerializeField]
    private PlayerController player;

    // Token: 0x04000016 RID: 22
    [SerializeField]
    private GameObject border;
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Token: 0x02000070 RID: 112
public class ChatController : MonoBehaviour
{
    // Token: 0x0600022F RID: 559 RVA: 0x0000A87A File Offset: 0x00008A7A
    private void OnEnable()
    {
        this.TMP_ChatInput.onSubmit.AddListener(new UnityAction<string>(this.AddToChatOutput));
    }

    // Token: 0x06000230 RID: 560 RVA: 0x0000A898 File Offset: 0x00008A98
    private void OnDisable()
    {
        this.TMP_ChatInput.onSubmit.RemoveListener(new UnityAction<string>(this.AddToChatOutput));
    }

    // Token: 0x06000231 RID: 561 RVA: 0x0000A8B8 File Offset: 0x00008AB8
    private void AddToChatOutput(string newText)
    {
        this.TMP_ChatInput.text = string.Empty;
        DateTime now = DateTime.Now;
        TMP_Text tmp_ChatOutput = this.TMP_ChatOutput;
        tmp_ChatOutput.text = string.Concat(new string[]
        {
            tmp_ChatOutput.text,
            "[<#FFFF80>",
            now.Hour.ToString("d2"),
            ":",
            now.Minute.ToString("d2"),
            ":",
            now.Second.ToString("d2"),
            "</color>] ",
            newText,
            "\n"
        });
        this.TMP_ChatInput.ActivateInputField();
        this.ChatScrollbar.value = 0f;
    }

    // Token: 0x040002A2 RID: 674
    public TMP_InputField TMP_ChatInput;

    // Token: 0x040002A3 RID: 675
    public TMP_Text TMP_ChatOutput;

    // Token: 0x040002A4 RID: 676
    public Scrollbar ChatScrollbar;
}

using System;
using UnityEngine;

// Token: 0x0200001D RID: 29
public class CheckPoint : MonoBehaviour
{
    // Token: 0x0600007A RID: 122 RVA: 0x000035FC File Offset: 0x000017FC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !this.alreadySaved)
        {
            this.alreadySaved = true;
            this.collector = other.transform.root.GetComponent<ItemCollector>();
            this.SaveData();
        }
    }

    // Token: 0x0600007B RID: 123 RVA: 0x00003636 File Offset: 0x00001836
    private void SaveData()
    {
        this.collector.SaveMushrooms();
        this.collector.GetComponent<PlayerController>().SavePosition();
        Object.FindObjectOfType<MenuExit>(true).SaveItemPresence();
    }

    // Token: 0x04000064 RID: 100
    public Transform point;

    // Token: 0x04000065 RID: 101
    private ItemCollector collector;

    // Token: 0x04000066 RID: 102
    private bool alreadySaved;
}

using System;
using UnityEngine;

// Token: 0x02000008 RID: 8
public static class CursorController
{
    // Token: 0x06000019 RID: 25 RVA: 0x000024C0 File Offset: 0x000006C0
    public static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Token: 0x0600001A RID: 26 RVA: 0x000024CE File Offset: 0x000006CE
    public static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

using System;
using UnityEngine;

// Token: 0x02000046 RID: 70
public class CurveSpawnedObject : MonoBehaviour
{
    // Token: 0x0600016E RID: 366 RVA: 0x000072A7 File Offset: 0x000054A7
    private void OnDestroy()
    {
        this.curveSpawner.OnSpawnedObjectDestroy(this);
    }

    // Token: 0x040001B3 RID: 435
    [HideInInspector]
    public CurveSpawner curveSpawner;
}

using System;
using System.Collections.Generic;
using UnityEngine;

// Token: 0x0200003B RID: 59
public class CurveSpawner : MonoBehaviour
{
    // Token: 0x1700000F RID: 15
    // (get) Token: 0x06000140 RID: 320 RVA: 0x00006901 File Offset: 0x00004B01
    private float startSpawnTime
    {
        get
        {
            return this.decreaseCurve.Evaluate(this._time);
        }
    }

    // Token: 0x06000141 RID: 321 RVA: 0x00006914 File Offset: 0x00004B14
    private void Start()
    {
        this.spawnTime = this.startSpawnTime;
    }

    // Token: 0x06000142 RID: 322 RVA: 0x00006924 File Offset: 0x00004B24
    private void Update()
    {
        if (this.isSpawning)
        {
            this.showTime = this.startSpawnTime;
            this.spawnTime -= Time.deltaTime;
            this._time += Time.deltaTime;
            if (this.spawnTime <= 0f)
            {
                int num = Random.Range(0, this.spawnPoints.Length);
                int num2 = Random.Range(0, this.objectsToSpawn.Length);
                CurveSpawnedObject component = Object.Instantiate<GameObject>(this.objectsToSpawn[num2], this.spawnPoints[num].position, this.spawnPoints[num].rotation).GetComponent<CurveSpawnedObject>();
                component.curveSpawner = this;
                this.spawnedObjects.Add(component);
                this.spawnTime = this.startSpawnTime;
            }
            if (this._time >= this.totalTime)
            {
                this.isSpawning = false;
            }
        }
    }

    // Token: 0x06000143 RID: 323 RVA: 0x000069F8 File Offset: 0x00004BF8
    public void OnSpawnedObjectDestroy(CurveSpawnedObject destroyedObject)
    {
        this.spawnedObjects.Remove(destroyedObject);
        if (this.spawnedObjects.Count == 0 && !this.isSpawning)
        {
            Action action = this.hasDoneSpawning;
            if (action != null)
            {
                action();
            }
            base.enabled = false;
        }
    }

    // Token: 0x06000144 RID: 324 RVA: 0x00006A34 File Offset: 0x00004C34
    public void OnEnable()
    {
        this._time = 0f;
        this.spawnTime = this.startSpawnTime;
        this.isSpawning = true;
    }

    // Token: 0x04000184 RID: 388
    public Transform[] spawnPoints;

    // Token: 0x04000185 RID: 389
    public GameObject[] objectsToSpawn;

    // Token: 0x04000186 RID: 390
    public AnimationCurve decreaseCurve;

    // Token: 0x04000187 RID: 391
    public Action hasDoneSpawning;

    // Token: 0x04000188 RID: 392
    private float _time;

    // Token: 0x04000189 RID: 393
    public float showTime;

    // Token: 0x0400018A RID: 394
    public float totalTime;

    // Token: 0x0400018B RID: 395
    public List<CurveSpawnedObject> spawnedObjects;

    // Token: 0x0400018C RID: 396
    private float spawnTime;

    // Token: 0x0400018D RID: 397
    private bool isSpawning;
}

using System;
using UnityEngine;

// Token: 0x02000067 RID: 103
public class CustomBullet : MonoBehaviour
{
    // Token: 0x0600020C RID: 524 RVA: 0x0000A083 File Offset: 0x00008283
    private void Start()
    {
        this.Setup();
    }

    // Token: 0x0600020D RID: 525 RVA: 0x0000A08B File Offset: 0x0000828B
    private void Update()
    {
        if (this.collisions > this.maxCollisions)
        {
            this.Explode();
        }
        this.maxLifetime -= Time.deltaTime;
        if (this.maxLifetime <= 0f)
        {
            this.Explode();
        }
    }

    // Token: 0x0600020E RID: 526 RVA: 0x0000A0C8 File Offset: 0x000082C8
    private void Explode()
    {
        if (this.explosion != null)
        {
            Object.Instantiate<GameObject>(this.explosion, base.transform.position, Quaternion.identity);
        }
        Collider[] array = Physics.OverlapSphere(base.transform.position, this.explosionRange, this.whatIsEnemies);
        for (int i = 0; i < array.Length; i++)
        {
            Enemy enemy;
            FoxShootable foxShootable;
            Melon melon;
            BooBoss booBoss;
            Candle candle;
            if (array[i].transform.root.TryGetComponent<Enemy>(out enemy))
            {
                enemy.TakeDamage(this.explosionDamage);
            }
            else if (array[i].TryGetComponent<FoxShootable>(out foxShootable))
            {
                foxShootable.TakeDamage(this.explosionDamage);
            }
            else if (array[i].TryGetComponent<Melon>(out melon))
            {
                melon.TakeDamage(this.explosionDamage);
            }
            else if (array[i].transform.root.TryGetComponent<BooBoss>(out booBoss))
            {
                if (!booBoss.forceField.activeInHierarchy)
                {
                    booBoss.TakeDamage(this.explosionDamage);
                }
            }
            else if (array[i].TryGetComponent<Candle>(out candle))
            {
                candle.BlowOutCandle();
            }
        }
        Object.Destroy(base.gameObject);
    }

    // Token: 0x0600020F RID: 527 RVA: 0x0000A1DF File Offset: 0x000083DF
    private void Delay()
    {
        Object.Destroy(base.gameObject);
    }

    // Token: 0x06000210 RID: 528 RVA: 0x0000A1EC File Offset: 0x000083EC
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Bullet"))
        {
            return;
        }
        this.collisions++;
        if (collision.collider.CompareTag("Enemy") && this.explodeOnTouch)
        {
            this.Explode();
        }
    }

    // Token: 0x06000211 RID: 529 RVA: 0x0000A23C File Offset: 0x0000843C
    private void Setup()
    {
        this.physics_mat = new PhysicMaterial();
        this.physics_mat.bounciness = this.bounciness;
        this.physics_mat.frictionCombine = PhysicMaterialCombine.Minimum;
        this.physics_mat.bounceCombine = PhysicMaterialCombine.Maximum;
        base.GetComponent<SphereCollider>().material = this.physics_mat;
        this.rb.useGravity = this.useGravity;
    }

    // Token: 0x06000212 RID: 530 RVA: 0x0000A29F File Offset: 0x0000849F
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.explosionRange);
    }

    // Token: 0x04000276 RID: 630
    public Rigidbody rb;

    // Token: 0x04000277 RID: 631
    public GameObject explosion;

    // Token: 0x04000278 RID: 632
    public LayerMask whatIsEnemies;

    // Token: 0x04000279 RID: 633
    [Range(0f, 1f)]
    public float bounciness;

    // Token: 0x0400027A RID: 634
    public bool useGravity;

    // Token: 0x0400027B RID: 635
    public int explosionDamage;

    // Token: 0x0400027C RID: 636
    public float explosionRange;

    // Token: 0x0400027D RID: 637
    public float explosionForce;

    // Token: 0x0400027E RID: 638
    public int maxCollisions;

    // Token: 0x0400027F RID: 639
    public float maxLifetime;

    // Token: 0x04000280 RID: 640
    public bool explodeOnTouch = true;

    // Token: 0x04000281 RID: 641
    private int collisions;

    // Token: 0x04000282 RID: 642
    private PhysicMaterial physics_mat;
}

using System;
using UnityEngine;

// Token: 0x02000009 RID: 9
public class CustomHint : MonoBehaviour
{
    // Token: 0x0600001B RID: 27 RVA: 0x000024DC File Offset: 0x000006DC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.hint.SetActive(true);
        }
    }

    // Token: 0x0600001C RID: 28 RVA: 0x000024F7 File Offset: 0x000006F7
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.hint.SetActive(false);
        }
    }

    // Token: 0x0600001D RID: 29 RVA: 0x00002512 File Offset: 0x00000712
    public void DeactivateTotally()
    {
        this.hint.SetActive(false);
        base.enabled = false;
    }

    // Token: 0x04000017 RID: 23
    public GameObject hint;
}

using System;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x0200001E RID: 30
public class CutoffMaskUI : Image
{
    // Token: 0x1700000C RID: 12
    // (get) Token: 0x0600007D RID: 125 RVA: 0x00003666 File Offset: 0x00001866
    public override Material materialForRendering
    {
        get
        {
            Material material = new Material(base.materialForRendering);
            material.SetInt("_StencilComp", 6);
            return material;
        }
    }
}

using System;
using UnityEngine;

// Token: 0x02000047 RID: 71
public class CutsceneChecker : MonoBehaviour
{
    // Token: 0x06000170 RID: 368 RVA: 0x000072C0 File Offset: 0x000054C0
    private void Start()
    {
        this._hasCompletedCutscene = PlayerPrefs.GetString("cutscene", "false");
        this._hasMetBooBoss = PlayerPrefs.GetString("hasMetBooBoss", "false");
        this._hasSeenTip = PlayerPrefs.GetString("hasSeenTip", "false");
        if (this._hasCompletedCutscene == "false")
        {
            this.player.SetActive(false);
            this.cutsceneObject.SetActive(true);
        }
        else if (this._hasCompletedCutscene == "true")
        {
            this.cutsceneObject.SetActive(false);
            this.player.SetActive(true);
        }
        this.boss.hasCompletedCutscene = (this._hasMetBooBoss == "true");
        this.boss.hasSeenTip = (this._hasSeenTip == "true");
    }

    // Token: 0x06000171 RID: 369 RVA: 0x000073A4 File Offset: 0x000055A4
    public void CompleteCutscene()
    {
        this._hasCompletedCutscene = "true";
        PlayerPrefs.SetString("cutscene", "true");
    }

    // Token: 0x06000172 RID: 370 RVA: 0x000073C0 File Offset: 0x000055C0
    public void MeetBooBoss()
    {
        this._hasMetBooBoss = "true";
        PlayerPrefs.SetString("hasMetBooBoss", "true");
    }

    // Token: 0x06000173 RID: 371 RVA: 0x000073DC File Offset: 0x000055DC
    public void HasSeenTip()
    {
        this._hasSeenTip = "true";
        PlayerPrefs.SetString("hasSeenTip", "true");
    }

    // Token: 0x040001B4 RID: 436
    [SerializeField]
    private GameObject cutsceneObject;

    // Token: 0x040001B5 RID: 437
    [SerializeField]
    private GameObject player;

    // Token: 0x040001B6 RID: 438
    [SerializeField]
    private BooBoss boss;

    // Token: 0x040001B7 RID: 439
    private string _hasCompletedCutscene;

    // Token: 0x040001B8 RID: 440
    private string _hasMetBooBoss;

    // Token: 0x040001B9 RID: 441
    private string _hasSeenTip;
}

using System;
using UnityEngine;

// Token: 0x02000068 RID: 104
public class DamageDealer : MonoBehaviour
{
    // Token: 0x06000214 RID: 532 RVA: 0x0000A2D0 File Offset: 0x000084D0
    private void OnTriggerEnter(Collider other)
    {
        PlayerHealth playerHealth;
        if (other.transform.root.TryGetComponent<PlayerHealth>(out playerHealth))
        {
            playerHealth.TakeDamage(this.damage);
        }
    }

    // Token: 0x06000215 RID: 533 RVA: 0x0000A300 File Offset: 0x00008500
    private void OnParticleCollision(GameObject other)
    {
        PlayerHealth playerHealth;
        if (other.transform.root.TryGetComponent<PlayerHealth>(out playerHealth))
        {
            playerHealth.TakeDamage(this.damage);
        }
    }

    // Token: 0x04000283 RID: 643
    public float damage;
}

using System;
using MEET_AND_TALK;
using TMPro;
using UnityEngine;

// Token: 0x02000075 RID: 117
[RequireComponent(typeof(CharacterController))]
public class DemoPlayer : MonoBehaviour
{
    // Token: 0x0600023D RID: 573 RVA: 0x0000AB4A File Offset: 0x00008D4A
    private void Start()
    {
        this.characterController = base.GetComponent<CharacterController>();
    }

    // Token: 0x0600023E RID: 574 RVA: 0x0000AB58 File Offset: 0x00008D58
    public void SetupInteractable(bool mode)
    {
        this.Interactable = mode;
    }

    // Token: 0x0600023F RID: 575 RVA: 0x0000AB64 File Offset: 0x00008D64
    private void Update()
    {
        if (this.Interactable)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Vector3 a = base.transform.TransformDirection(Vector3.forward);
            Vector3 a2 = base.transform.TransformDirection(Vector3.right);
            bool key = Input.GetKey(KeyCode.LeftShift);
            float d = this.canMove ? ((key ? this.runSpeed : this.walkSpeed) * Input.GetAxis("Vertical")) : 0f;
            float d2 = this.canMove ? ((key ? this.runSpeed : this.walkSpeed) * Input.GetAxis("Horizontal")) : 0f;
            float y = this.moveDirection.y;
            this.moveDirection = a * d + a2 * d2;
            if (Input.GetButton("Jump") && this.canMove && this.characterController.isGrounded)
            {
                this.moveDirection.y = this.jumpPower;
            }
            else
            {
                this.moveDirection.y = y;
            }
            if (!this.characterController.isGrounded)
            {
                this.moveDirection.y = this.moveDirection.y - this.gravity * Time.deltaTime;
            }
            this.characterController.Move(this.moveDirection * Time.deltaTime);
            if (this.canMove)
            {
                this.rotationX += -Input.GetAxis("Mouse Y") * this.lookSpeed;
                this.rotationX = Mathf.Clamp(this.rotationX, -this.lookXLimit, this.lookXLimit);
                this.playerCamera.transform.localRotation = Quaternion.Euler(this.rotationX, 0f, 0f);
                base.transform.rotation *= Quaternion.Euler(0f, Input.GetAxis("Mouse X") * this.lookSpeed, 0f);
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        this.Lanuage.text = string.Format("Language: {0}", this.localizationManager.SelectedLang());
        if (Input.GetKeyDown(KeyCode.U))
        {
            this.localizationManager.selectedLang = SystemLanguage.English;
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            this.localizationManager.selectedLang = SystemLanguage.Polish;
        }
    }

    // Token: 0x040002B0 RID: 688
    public LocalizationManager localizationManager;

    // Token: 0x040002B1 RID: 689
    public bool Interactable;

    // Token: 0x040002B2 RID: 690
    public Camera playerCamera;

    // Token: 0x040002B3 RID: 691
    public float walkSpeed = 6f;

    // Token: 0x040002B4 RID: 692
    public float runSpeed = 12f;

    // Token: 0x040002B5 RID: 693
    public float jumpPower = 7f;

    // Token: 0x040002B6 RID: 694
    public float gravity = 10f;

    // Token: 0x040002B7 RID: 695
    public TMP_Text Lanuage;

    // Token: 0x040002B8 RID: 696
    public float lookSpeed = 2f;

    // Token: 0x040002B9 RID: 697
    public float lookXLimit = 45f;

    // Token: 0x040002BA RID: 698
    private Vector3 moveDirection = Vector3.zero;

    // Token: 0x040002BB RID: 699
    private float rotationX;

    // Token: 0x040002BC RID: 700
    public bool canMove = true;

    // Token: 0x040002BD RID: 701
    private CharacterController characterController;
}

using System;
using UnityEngine;

// Token: 0x02000029 RID: 41
public class Destroyer : MonoBehaviour
{
    // Token: 0x060000E2 RID: 226 RVA: 0x000051BB File Offset: 0x000033BB
    private void Start()
    {
        Object.Destroy(base.gameObject, this.lifetime);
    }

    // Token: 0x040000F8 RID: 248
    public float lifetime;
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// Token: 0x0200001F RID: 31
public class DialogueSwitch : MonoBehaviour
{
    // Token: 0x0600007F RID: 127 RVA: 0x00003688 File Offset: 0x00001888
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (this.firstTime)
            {
                this.dialogues[0].Invoke();
                this.firstTime = false;
                return;
            }
            if (this.collector.items < 20)
            {
                this.dialogues[1].Invoke();
                return;
            }
            if (this.collector.items == 20)
            {
                this.dialogues[2].Invoke();
                base.Invoke("MakeAFace", 3.3f);
                return;
            }
            if (this.collector.items > 20)
            {
                this.dialogues[3].Invoke();
            }
        }
    }

    // Token: 0x06000080 RID: 128 RVA: 0x00003728 File Offset: 0x00001928
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.skip.Invoke();
        }
    }

    // Token: 0x06000081 RID: 129 RVA: 0x00003744 File Offset: 0x00001944
    private void MakeAFace()
    {
        this.trollAnimator.SetBool("hasChanged", true);
        this.fenceAnimator.SetBool("hasTrolled", true);
        this.tableText.text = "вход = 21";
        base.GetComponent<SphereCollider>().enabled = false;
        base.Invoke("StartFight", 4f);
    }

    // Token: 0x06000082 RID: 130 RVA: 0x0000379F File Offset: 0x0000199F
    private void StartFight()
    {
        base.GetComponent<TrollThrower>().enabled = true;
        if (this.tip != null)
        {
            this.tip.SetActive(true);
        }
    }

    // Token: 0x06000083 RID: 131 RVA: 0x000037C7 File Offset: 0x000019C7
    private void OnDestroy()
    {
        this.flower.SetActive(true);
    }

    // Token: 0x04000067 RID: 103
    public bool firstTime;

    // Token: 0x04000068 RID: 104
    public ItemCollector collector;

    // Token: 0x04000069 RID: 105
    public UnityEvent[] dialogues;

    // Token: 0x0400006A RID: 106
    public UnityEvent skip;

    // Token: 0x0400006B RID: 107
    public Animator trollAnimator;

    // Token: 0x0400006C RID: 108
    public Animator fenceAnimator;

    // Token: 0x0400006D RID: 109
    public TextMeshPro tableText;

    // Token: 0x0400006E RID: 110
    public GameObject flower;

    // Token: 0x0400006F RID: 111
    public GameObject tip;
}

using System;
using UnityEngine;

// Token: 0x0200000A RID: 10
public class DomeMelonController : MonoBehaviour
{
    // Token: 0x0600001F RID: 31 RVA: 0x00002530 File Offset: 0x00000730
    private void OnEnable()
    {
        MelonfallManager melonfallManager = this.melonfall;
        melonfallManager.hasCompletedMelonfall = (Action)Delegate.Combine(melonfallManager.hasCompletedMelonfall, new Action(this.OnMelonfallFinished));
        CurveSpawner curveSpawner = this.curveSpawner;
        curveSpawner.hasDoneSpawning = (Action)Delegate.Combine(curveSpawner.hasDoneSpawning, new Action(this.OnSpawnerFinished));
    }

    // Token: 0x06000020 RID: 32 RVA: 0x0000258C File Offset: 0x0000078C
    private void OnDisable()
    {
        MelonfallManager melonfallManager = this.melonfall;
        melonfallManager.hasCompletedMelonfall = (Action)Delegate.Remove(melonfallManager.hasCompletedMelonfall, new Action(this.OnMelonfallFinished));
        CurveSpawner curveSpawner = this.curveSpawner;
        curveSpawner.hasDoneSpawning = (Action)Delegate.Remove(curveSpawner.hasDoneSpawning, new Action(this.OnSpawnerFinished));
    }

    // Token: 0x06000021 RID: 33 RVA: 0x000025E7 File Offset: 0x000007E7
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !this._hasSteppedInCenter)
        {
            this._hasSteppedInCenter = true;
            this.floorGlassAnimator.SetTrigger("hasStepped");
            GravityController.instance.SetEarthGravity();
        }
    }

    // Token: 0x06000022 RID: 34 RVA: 0x0000261F File Offset: 0x0000081F
    public void OnMelonfallFinished()
    {
        this.curveSpawner.enabled = true;
    }

    // Token: 0x06000023 RID: 35 RVA: 0x0000262D File Offset: 0x0000082D
    public void OnSpawnerFinished()
    {
        this.floorCylinderCollider.enabled = false;
        this.sign.SetActive(true);
        this.floorGlassAnimator.SetTrigger("hasFinished");
        MusicManager.instance.EnableMusic(0);
        base.enabled = false;
    }

    // Token: 0x04000018 RID: 24
    [SerializeField]
    private MelonfallManager melonfall;

    // Token: 0x04000019 RID: 25
    [SerializeField]
    private CurveSpawner curveSpawner;

    // Token: 0x0400001A RID: 26
    [SerializeField]
    private Collider floorCylinderCollider;

    // Token: 0x0400001B RID: 27
    [SerializeField]
    private Animator floorGlassAnimator;

    // Token: 0x0400001C RID: 28
    [SerializeField]
    private GameObject sign;

    // Token: 0x0400001D RID: 29
    private bool _hasSteppedInCenter;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x0200002A RID: 42
public class DoorInteraction : MonoBehaviour
{
    // Token: 0x060000E4 RID: 228 RVA: 0x000051D6 File Offset: 0x000033D6
    private void Update()
    {
        this.playerNearDoor = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        if (this.playerNearDoor && Input.GetKeyDown(KeyCode.E))
        {
            this.OpenDoor();
        }
    }

    // Token: 0x060000E5 RID: 229 RVA: 0x00005218 File Offset: 0x00003418
    public void OpenDoor()
    {
        this.doorAnimator.SetBool("isOpened", true);
        this.gateAnimator.SetBool("hasMushrooms", true);
        this.enemyRun.speed = 8f;
        this.enemyRun.GetComponent<AudioSource>().Play();
        Object.FindObjectOfType<MusicManager>().EnableMusic(3);
        base.enabled = false;
    }

    // Token: 0x060000E6 RID: 230 RVA: 0x00005279 File Offset: 0x00003479
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x040000F9 RID: 249
    public Animator doorAnimator;

    // Token: 0x040000FA RID: 250
    public Animator gateAnimator;

    // Token: 0x040000FB RID: 251
    public NavMeshAgent enemyRun;

    // Token: 0x040000FC RID: 252
    public LayerMask whatIsPlayer;

    // Token: 0x040000FD RID: 253
    public float sightRange;

    // Token: 0x040000FE RID: 254
    private bool playerNearDoor;
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Token: 0x02000011 RID: 17
public class DynamicJoystick : Joystick
{
    // Token: 0x17000009 RID: 9
    // (get) Token: 0x06000049 RID: 73 RVA: 0x00002D1C File Offset: 0x00000F1C
    // (set) Token: 0x0600004A RID: 74 RVA: 0x00002D24 File Offset: 0x00000F24
    public float MoveThreshold
    {
        get
        {
            return this.moveThreshold;
        }
        set
        {
            this.moveThreshold = Mathf.Abs(value);
        }
    }

    // Token: 0x0600004B RID: 75 RVA: 0x00002D32 File Offset: 0x00000F32
    protected override void Start()
    {
        this.MoveThreshold = this.moveThreshold;
        base.Start();
        this.background.gameObject.SetActive(false);
    }

    // Token: 0x0600004C RID: 76 RVA: 0x00002D57 File Offset: 0x00000F57
    public override void OnPointerDown(PointerEventData eventData)
    {
        this.background.anchoredPosition = base.ScreenPointToAnchoredPosition(eventData.position);
        this.background.gameObject.SetActive(true);
        base.OnPointerDown(eventData);
    }

    // Token: 0x0600004D RID: 77 RVA: 0x00002D88 File Offset: 0x00000F88
    public override void OnPointerUp(PointerEventData eventData)
    {
        this.background.gameObject.SetActive(false);
        base.OnPointerUp(eventData);
    }

    // Token: 0x0600004E RID: 78 RVA: 0x00002DA4 File Offset: 0x00000FA4
    protected override void HandleInput(float magnitude, Vector2 normalised, Vector2 radius, Camera cam)
    {
        if (magnitude > this.moveThreshold)
        {
            Vector2 b = normalised * (magnitude - this.moveThreshold) * radius;
            this.background.anchoredPosition += b;
        }
        base.HandleInput(magnitude, normalised, radius, cam);
    }

    // Token: 0x0400003C RID: 60
    [SerializeField]
    private float moveThreshold = 1f;
}

using System;
using UnityEngine;

// Token: 0x02000074 RID: 116
[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class EnableDepthBuffer : MonoBehaviour
{
    // Token: 0x0600023B RID: 571 RVA: 0x0000AB0D File Offset: 0x00008D0D
    private void Update()
    {
        if (this.m_camera == null)
        {
            this.m_camera = base.GetComponent<Camera>();
        }
        if (this.m_camera.depthTextureMode == DepthTextureMode.None)
        {
            this.m_camera.depthTextureMode = DepthTextureMode.Depth;
        }
    }

    // Token: 0x040002AF RID: 687
    private Camera m_camera;
}

using System;
using UnityEngine;

// Token: 0x0200002B RID: 43
public class EndGame : MonoBehaviour
{
    // Token: 0x060000E8 RID: 232 RVA: 0x000052A4 File Offset: 0x000034A4
    public void FadeToTitles()
    {
        this.magicPickSound.Play();
        MusicManager.instance.EnableMusic(6);
        base.gameObject.SetActive(false);
        AfterTitles[] array = Object.FindObjectsOfType<AfterTitles>(true);
        for (int i = 0; i < array.Length; i++)
        {
            array[i].gameObject.SetActive(true);
        }
    }

    // Token: 0x040000FF RID: 255
    public AudioSource magicPickSound;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x02000024 RID: 36
public class Enemy : MonoBehaviour
{
    // Token: 0x060000BD RID: 189 RVA: 0x0000479D File Offset: 0x0000299D
    private void Awake()
    {
        this.player = GameObject.FindGameObjectWithTag("Player").transform.root;
        this.agent = base.GetComponent<NavMeshAgent>();
    }

    // Token: 0x060000BE RID: 190 RVA: 0x000047C8 File Offset: 0x000029C8
    private void Update()
    {
        this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
        if (!this.playerInSightRange && !this.playerInAttackRange)
        {
            this.Patroling();
        }
        if (this.playerInSightRange && !this.playerInAttackRange)
        {
            this.ChasePlayer();
        }
        if (this.playerInAttackRange && this.playerInSightRange)
        {
            this.AttackPlayer();
        }
    }

    // Token: 0x060000BF RID: 191 RVA: 0x00004868 File Offset: 0x00002A68
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x060000C0 RID: 192 RVA: 0x000048CC File Offset: 0x00002ACC
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x060000C1 RID: 193 RVA: 0x00004974 File Offset: 0x00002B74
    private void ChasePlayer()
    {
        if (Vector3.Distance(this.player.position, this.lastPlayerPosition) > 0.5f)
        {
            this.agent.SetDestination(this.player.position);
            this.lastPlayerPosition = this.player.position;
        }
    }

    // Token: 0x060000C2 RID: 194 RVA: 0x000049C8 File Offset: 0x00002BC8
    private void AttackPlayer()
    {
        if (Vector3.Distance(this.player.position, this.lastPlayerPosition) > 0.5f)
        {
            this.agent.SetDestination(this.player.position);
            this.lastPlayerPosition = this.player.position;
        }
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x060000C3 RID: 195 RVA: 0x00004A91 File Offset: 0x00002C91
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x060000C4 RID: 196 RVA: 0x00004A9A File Offset: 0x00002C9A
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x060000C5 RID: 197 RVA: 0x00004AC8 File Offset: 0x00002CC8
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x060000C6 RID: 198 RVA: 0x00004AF4 File Offset: 0x00002CF4
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x040000C0 RID: 192
    public NavMeshAgent agent;

    // Token: 0x040000C1 RID: 193
    public Transform player;

    // Token: 0x040000C2 RID: 194
    public LayerMask whatIsGround;

    // Token: 0x040000C3 RID: 195
    public LayerMask whatIsPlayer;

    // Token: 0x040000C4 RID: 196
    public Vector3 walkPoint;

    // Token: 0x040000C5 RID: 197
    private bool walkPointSet;

    // Token: 0x040000C6 RID: 198
    public float walkPointRange;

    // Token: 0x040000C7 RID: 199
    public float timeBetweenAttacks;

    // Token: 0x040000C8 RID: 200
    private bool alreadyAttacked;

    // Token: 0x040000C9 RID: 201
    public float sightRange;

    // Token: 0x040000CA RID: 202
    public float attackRange;

    // Token: 0x040000CB RID: 203
    public bool playerInSightRange;

    // Token: 0x040000CC RID: 204
    public bool playerInAttackRange;

    // Token: 0x040000CD RID: 205
    public float health;

    // Token: 0x040000CE RID: 206
    public int damage;

    // Token: 0x040000CF RID: 207
    public GameObject deathEffect;

    // Token: 0x040000D0 RID: 208
    private Vector3 lastPlayerPosition;
}

using System;
using UnityEngine;

// Token: 0x02000025 RID: 37
public class EnemySpawner : MonoBehaviour
{
    // Token: 0x060000C8 RID: 200 RVA: 0x00004B4C File Offset: 0x00002D4C
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            int num = Random.Range(0, this.spawnPoints.Length);
            int num2 = Random.Range(0, this.enemies.Length);
            Object.Instantiate<GameObject>(this.enemies[num2], this.spawnPoints[num].position, Quaternion.identity);
            base.enabled = false;
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x040000D1 RID: 209
    public Transform[] spawnPoints;

    // Token: 0x040000D2 RID: 210
    public GameObject[] enemies;
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Token: 0x02000071 RID: 113
public class EnvMapAnimator : MonoBehaviour
{
    // Token: 0x06000233 RID: 563 RVA: 0x0000A992 File Offset: 0x00008B92
    private void Awake()
    {
        this.m_textMeshPro = base.GetComponent<TMP_Text>();
        this.m_material = this.m_textMeshPro.fontSharedMaterial;
    }

    // Token: 0x06000234 RID: 564 RVA: 0x0000A9B1 File Offset: 0x00008BB1
    private IEnumerator Start()
    {
        Matrix4x4 matrix = default(Matrix4x4);
        for (; ; )
        {
            matrix.SetTRS(Vector3.zero, Quaternion.Euler(Time.time * this.RotationSpeeds.x, Time.time * this.RotationSpeeds.y, Time.time * this.RotationSpeeds.z), Vector3.one);
            this.m_material.SetMatrix("_EnvMatrix", matrix);
            yield return null;
        }
        yield break;
    }

    // Token: 0x040002A5 RID: 677
    public Vector3 RotationSpeeds;

    // Token: 0x040002A6 RID: 678
    private TMP_Text m_textMeshPro;

    // Token: 0x040002A7 RID: 679
    private Material m_material;
}

using System;

// Token: 0x02000012 RID: 18
public class FixedJoystick : Joystick
{
}

using System;
using UnityEngine.EventSystems;

// Token: 0x02000013 RID: 19
public class FloatingJoystick : Joystick
{
    // Token: 0x06000051 RID: 81 RVA: 0x00002E0C File Offset: 0x0000100C
    protected override void Start()
    {
        base.Start();
        this.background.gameObject.SetActive(false);
    }

    // Token: 0x06000052 RID: 82 RVA: 0x00002E25 File Offset: 0x00001025
    public override void OnPointerDown(PointerEventData eventData)
    {
        this.background.anchoredPosition = base.ScreenPointToAnchoredPosition(eventData.position);
        this.background.gameObject.SetActive(true);
        base.OnPointerDown(eventData);
    }

    // Token: 0x06000053 RID: 83 RVA: 0x00002E56 File Offset: 0x00001056
    public override void OnPointerUp(PointerEventData eventData)
    {
        this.background.gameObject.SetActive(false);
        base.OnPointerUp(eventData);
    }
}

using System;
using UnityEngine;

// Token: 0x0200003C RID: 60
public class FoodActivator : MonoBehaviour
{
    // Token: 0x06000146 RID: 326 RVA: 0x00006A5C File Offset: 0x00004C5C
    private void OnDestroy()
    {
        this.burger.StartCoroutine(this.burger.OnBurgerDestroyed());
    }

    // Token: 0x0400018E RID: 398
    public BurgerBehaviour burger;
}

using System;
using UnityEngine;
using UnityEngine.Events;

// Token: 0x02000048 RID: 72
public class FoxDialogueSwitch : MonoBehaviour
{
    // Token: 0x06000175 RID: 373 RVA: 0x00007400 File Offset: 0x00005600
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (!this.tierController.HasWon)
            {
                this.dialogues[0].Invoke();
                this.SwitchGateState(false);
                return;
            }
            if (this.tierController.HasWon && this.itemCollector.items >= 10)
            {
                this.dialogues[1].Invoke();
                this.SwitchGateState(true);
            }
        }
    }

    // Token: 0x06000176 RID: 374 RVA: 0x0000746C File Offset: 0x0000566C
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            this.skip.Invoke();
        }
    }

    // Token: 0x06000177 RID: 375 RVA: 0x00007482 File Offset: 0x00005682
    public void SwitchGateState(bool state)
    {
        this.gateAnimator.SetBool("isOpened", state);
    }

    // Token: 0x040001BA RID: 442
    [SerializeField]
    private FoxTierController tierController;

    // Token: 0x040001BB RID: 443
    [SerializeField]
    private UnityEvent[] dialogues;

    // Token: 0x040001BC RID: 444
    [SerializeField]
    private UnityEvent skip;

    // Token: 0x040001BD RID: 445
    [SerializeField]
    private Animator gateAnimator;

    // Token: 0x040001BE RID: 446
    [SerializeField]
    private ItemCollector itemCollector;
}

using System;
using UnityEngine;

// Token: 0x02000049 RID: 73
public class FoxShootable : MonoBehaviour
{
    // Token: 0x06000179 RID: 377 RVA: 0x0000749D File Offset: 0x0000569D
    private void Start()
    {
        this._foxTierController = Object.FindObjectOfType<FoxTierController>();
    }

    // Token: 0x0600017A RID: 378 RVA: 0x000074AA File Offset: 0x000056AA
    public void TakeDamage(int damage)
    {
        this.health -= damage;
        if (this.health <= 0)
        {
            this._foxTierController.DestroyFox(base.gameObject);
        }
    }

    // Token: 0x040001BF RID: 447
    [SerializeField]
    private int health;

    // Token: 0x040001C0 RID: 448
    public int FoxType;

    // Token: 0x040001C1 RID: 449
    private FoxTierController _foxTierController;
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Token: 0x0200004A RID: 74
public class FoxTierController : MonoBehaviour
{
    // Token: 0x0600017C RID: 380 RVA: 0x000074DC File Offset: 0x000056DC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.targetFoxType = 5;
            this.SpawnFoxes();
            this.hintText.text = "стреляйте только в указанных лисиц!";
            this.hintText.transform.root.GetComponent<Animator>().SetTrigger("isActivated");
            this.icon.SetActive(true);
        }
    }

    // Token: 0x0600017D RID: 381 RVA: 0x0000753E File Offset: 0x0000573E
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.icon.SetActive(false);
        }
    }

    // Token: 0x0600017E RID: 382 RVA: 0x0000755C File Offset: 0x0000575C
    public void SpawnFoxes()
    {
        this._targetFoxCount = 0;
        this.DestroyAllFoxes();
        this.spawnedFoxes = new List<GameObject>();
        this.targetFoxType = ((this.targetFoxType < 5) ? (this.targetFoxType + 1) : 0);
        GameObject[] array = this.targetFoxIcons;
        for (int i = 0; i < array.Length; i++)
        {
            array[i].SetActive(false);
        }
        this.targetFoxIcons[this.targetFoxType].SetActive(true);
        foreach (Transform transform in this.foxPositions)
        {
            int num = Random.Range(0, this.foxVariants.Length);
            FoxShootable component = Object.Instantiate<GameObject>(this.foxVariants[num], transform.position, transform.rotation).GetComponent<FoxShootable>();
            component.FoxType = num;
            if (num == this.targetFoxType)
            {
                this._targetFoxCount++;
            }
            this.spawnedFoxes.Add(component.gameObject);
        }
        this.targetFoxCountText.text = "x" + this._targetFoxCount.ToString();
        if (this._targetFoxCount == 0)
        {
            this.targetFoxType = ((this.targetFoxType > 0) ? (this.targetFoxType - 1) : 5);
            this.SpawnFoxes();
        }
    }

    // Token: 0x0600017F RID: 383 RVA: 0x00007694 File Offset: 0x00005894
    public void DestroyAllFoxes()
    {
        foreach (GameObject obj in this.spawnedFoxes)
        {
            Object.Destroy(obj);
        }
    }

    // Token: 0x06000180 RID: 384 RVA: 0x000076E4 File Offset: 0x000058E4
    public void DestroyFox(GameObject fox)
    {
        if (fox.GetComponent<FoxShootable>().FoxType == this.targetFoxType)
        {
            if (this._targetFoxCount > 1)
            {
                this._targetFoxCount--;
                this.targetFoxCountText.text = "x" + this._targetFoxCount.ToString();
            }
            else if (this.targetFoxType == 5)
            {
                this.hintText.text = "победа!";
                this._targetFoxCount--;
                this.targetFoxCountText.text = "x" + this._targetFoxCount.ToString();
                this.hintText.transform.root.GetComponent<Animator>().SetTrigger("isActivated");
                this.HasWon = true;
                this.DestroyAllFoxes();
                this.icon.SetActive(false);
                base.GetComponent<Collider>().enabled = false;
            }
            else
            {
                this.SpawnFoxes();
            }
        }
        else
        {
            this.targetFoxType = 5;
            this.hintText.text = "мимо! Попробуйте снова";
            this.hintText.transform.root.GetComponent<Animator>().SetTrigger("isActivated");
            this.SpawnFoxes();
        }
        if (this.spawnedFoxes.Contains(fox))
        {
            Object.Destroy(fox);
            this.spawnedFoxes.Remove(fox);
        }
    }

    // Token: 0x040001C2 RID: 450
    [SerializeField]
    private Transform[] foxPositions;

    // Token: 0x040001C3 RID: 451
    [SerializeField]
    private GameObject[] foxVariants;

    // Token: 0x040001C4 RID: 452
    [SerializeField]
    private GameObject[] targetFoxIcons;

    // Token: 0x040001C5 RID: 453
    [SerializeField]
    private GameObject icon;

    // Token: 0x040001C6 RID: 454
    [SerializeField]
    private List<GameObject> spawnedFoxes;

    // Token: 0x040001C7 RID: 455
    [SerializeField]
    private int targetFoxType;

    // Token: 0x040001C8 RID: 456
    [SerializeField]
    private TextMeshProUGUI targetFoxCountText;

    // Token: 0x040001C9 RID: 457
    [SerializeField]
    private TextMeshProUGUI hintText;

    // Token: 0x040001CA RID: 458
    public bool HasWon;

    // Token: 0x040001CB RID: 459
    private int _targetFoxCount;
}

using System;
using UnityEngine;

// Token: 0x0200004B RID: 75
public class FurnaceLighterTrigger : MonoBehaviour
{
    // Token: 0x06000182 RID: 386 RVA: 0x00007840 File Offset: 0x00005A40
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this._isInZone = true;
            this.hints[this._hasLit ? 1 : 0].SetActive(true);
            this.customHint.DeactivateTotally();
        }
    }

    // Token: 0x06000183 RID: 387 RVA: 0x0000787A File Offset: 0x00005A7A
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this._isInZone = false;
            this.hints[this._hasLit ? 1 : 0].SetActive(false);
        }
    }

    // Token: 0x06000184 RID: 388 RVA: 0x000078AC File Offset: 0x00005AAC
    private void Update()
    {
        if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return)) && this._isInZone)
        {
            if (this.collector.items > 0 && !this._hasLit)
            {
                this._hasLit = true;
                this.hints[0].SetActive(false);
                this.collector.items = 0;
                this.woodPanel.transform.GetChild(0).gameObject.SetActive(false);
                this.collector.itemText.text = string.Format("{0}", this.collector.items);
                this.collector.SaveMushrooms();
                this.fire.SetActive(true);
                this.colliders[0].enabled = false;
                this.colliders[1].enabled = true;
                return;
            }
            if (this._hasLit && !this._hasSat)
            {
                this._hasSat = true;
                this.player.isStopped = true;
                this.player.gameObject.SetActive(false);
                this.cutscene.SetActive(true);
                this.hints[1].SetActive(false);
            }
        }
    }

    // Token: 0x040001CC RID: 460
    [SerializeField]
    private GameObject[] hints;

    // Token: 0x040001CD RID: 461
    [SerializeField]
    private Collider[] colliders;

    // Token: 0x040001CE RID: 462
    [SerializeField]
    private GameObject fire;

    // Token: 0x040001CF RID: 463
    [SerializeField]
    private GameObject woodPanel;

    // Token: 0x040001D0 RID: 464
    [SerializeField]
    private GameObject cutscene;

    // Token: 0x040001D1 RID: 465
    [SerializeField]
    private PlayerController player;

    // Token: 0x040001D2 RID: 466
    [SerializeField]
    private ItemCollector collector;

    // Token: 0x040001D3 RID: 467
    [SerializeField]
    private CustomHint customHint;

    // Token: 0x040001D4 RID: 468
    private bool _isInZone;

    // Token: 0x040001D5 RID: 469
    public bool _hasLit;

    // Token: 0x040001D6 RID: 470
    public bool _hasSat;
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

// Token: 0x0200005E RID: 94
public class GameSettings : MonoBehaviour
{
    // Token: 0x060001DF RID: 479 RVA: 0x00009069 File Offset: 0x00007269
    private void Start()
    {
        this.LoadValues();
    }

    // Token: 0x060001E0 RID: 480 RVA: 0x00009071 File Offset: 0x00007271
    public void TextToSlider(float sensitivity)
    {
        this.sliderPercentUI.text = string.Format("чувствительность мыши: {0}%", Mathf.RoundToInt(sensitivity / 16f));
    }

    // Token: 0x060001E1 RID: 481 RVA: 0x00009099 File Offset: 0x00007299
    public void TickState()
    {
        if (this.postEffectsEnabled == 0)
        {
            this.tick.SetActive(false);
            this.postProcess.enabled = false;
            return;
        }
        this.tick.SetActive(true);
        this.postProcess.enabled = true;
    }

    // Token: 0x060001E2 RID: 482 RVA: 0x000090D4 File Offset: 0x000072D4
    public void SaveSensitivity()
    {
        float value = this.sensitivitySlider.value;
        PlayerPrefs.SetFloat("SensitivityValue", value);
        this.LoadValues();
    }

    // Token: 0x060001E3 RID: 483 RVA: 0x00009100 File Offset: 0x00007300
    public void SaveEffects()
    {
        if (this.tick.activeSelf)
        {
            this.tick.SetActive(false);
            this.postEffectsEnabled = 0;
        }
        else
        {
            this.tick.SetActive(true);
            this.postEffectsEnabled = 1;
        }
        PlayerPrefs.SetInt("PostEffects", this.postEffectsEnabled);
        this.LoadValues();
    }

    // Token: 0x060001E4 RID: 484 RVA: 0x00009158 File Offset: 0x00007358
    public void SaveQuality(int index)
    {
        this.graphicsQuality = index;
        PlayerPrefs.SetInt("GraphicsQuality", this.graphicsQuality);
    }

    // Token: 0x060001E5 RID: 485 RVA: 0x00009174 File Offset: 0x00007374
    public void LoadValues()
    {
        float num = 400f;
        int num2 = 1;
        int qualityLevel = 2;
        if (PlayerPrefs.HasKey("SensitivityValue"))
        {
            num = PlayerPrefs.GetFloat("SensitivityValue");
        }
        if (PlayerPrefs.HasKey("PostEffects"))
        {
            num2 = PlayerPrefs.GetInt("PostEffects");
        }
        if (PlayerPrefs.HasKey("GraphicsQuality"))
        {
            qualityLevel = PlayerPrefs.GetInt("GraphicsQuality");
        }
        this.sensitivitySlider.value = num;
        this.postEffectsEnabled = num2;
        this.graphicsQuality = qualityLevel;
        this.qualityDropdown.SetQualityLevel(qualityLevel);
        this.TextToSlider(num);
        this.TickState();
        this.playerCamera.sensitivityX = (this.playerCamera.sensitivityY = num);
    }

    // Token: 0x04000252 RID: 594
    [SerializeField]
    private Slider sensitivitySlider;

    // Token: 0x04000253 RID: 595
    [SerializeField]
    private PostProcessLayer postProcess;

    // Token: 0x04000254 RID: 596
    [SerializeField]
    private PlayerCamera playerCamera;

    // Token: 0x04000255 RID: 597
    [SerializeField]
    private TextMeshProUGUI sliderPercentUI;

    // Token: 0x04000256 RID: 598
    [SerializeField]
    private SetQuality qualityDropdown;

    // Token: 0x04000257 RID: 599
    public int postEffectsEnabled;

    // Token: 0x04000258 RID: 600
    public int graphicsQuality;

    // Token: 0x04000259 RID: 601
    [SerializeField]
    private GameObject tick;
}

using System;
using UnityEngine;

// Token: 0x0200003D RID: 61
public class GIFController : MonoBehaviour
{
    // Token: 0x06000148 RID: 328 RVA: 0x00006A7D File Offset: 0x00004C7D
    private void Start()
    {
    }

    // Token: 0x06000149 RID: 329 RVA: 0x00006A7F File Offset: 0x00004C7F
    private void Update()
    {
    }
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Token: 0x0200002F RID: 47
public class GravityController : MonoBehaviour
{
    // Token: 0x060000F4 RID: 244 RVA: 0x00005455 File Offset: 0x00003655
    private void Awake()
    {
        if (GravityController.instance != null)
        {
            Object.Destroy(base.gameObject);
            return;
        }
        GravityController.instance = this;
        Object.DontDestroyOnLoad(base.transform.gameObject);
    }

    // Token: 0x060000F5 RID: 245 RVA: 0x00005488 File Offset: 0x00003688
    public void CheckGravity()
    {
        if (SceneManager.GetActiveScene().buildIndex == 2)
        {
            this.SetMoonGravity();
            return;
        }
        if (SceneManager.GetActiveScene().buildIndex == 1)
        {
            this.SetEarthGravity();
        }
    }

    // Token: 0x060000F6 RID: 246 RVA: 0x000054C2 File Offset: 0x000036C2
    public void SetMoonGravity()
    {
        Physics.gravity = this.moonGravity;
        this.player.jumpForce = this.moonJumpForce;
    }

    // Token: 0x060000F7 RID: 247 RVA: 0x000054E0 File Offset: 0x000036E0
    public void SetEarthGravity()
    {
        Physics.gravity = this.earthGravity;
        this.player.jumpForce = this.earthJumpForce;
    }

    // Token: 0x04000109 RID: 265
    public static GravityController instance;

    // Token: 0x0400010A RID: 266
    [SerializeField]
    private Vector3 moonGravity;

    // Token: 0x0400010B RID: 267
    [SerializeField]
    private Vector3 earthGravity;

    // Token: 0x0400010C RID: 268
    public PlayerController player;

    // Token: 0x0400010D RID: 269
    [SerializeField]
    private float earthJumpForce;

    // Token: 0x0400010E RID: 270
    [SerializeField]
    private float moonJumpForce;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x0200002C RID: 44
public class GribmanActivator : MonoBehaviour
{
    // Token: 0x060000EA RID: 234 RVA: 0x000052FE File Offset: 0x000034FE
    private void Start()
    {
        Object.Instantiate<GameObject>(this.gribmanSound, base.transform.position, Quaternion.identity);
        base.Invoke("AttackPlayer", 8.5f);
    }

    // Token: 0x060000EB RID: 235 RVA: 0x0000532C File Offset: 0x0000352C
    public void AttackPlayer()
    {
        base.GetComponent<NavMeshAgent>().speed = 10f;
        Object.Instantiate<GameObject>(this.attackSound, base.transform.position, Quaternion.identity);
        base.Invoke("AttackPlayer", 8.5f);
    }

    // Token: 0x04000100 RID: 256
    public GameObject gribmanSound;

    // Token: 0x04000101 RID: 257
    public GameObject attackSound;
}

using System;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x0200000B RID: 11
public class GridLayoutManager : MonoBehaviour
{
    // Token: 0x06000025 RID: 37 RVA: 0x00002671 File Offset: 0x00000871
    private void Start()
    {
        this.ConfigureGrid(2, 4, 150f, 150f, 10f, 10f);
    }

    // Token: 0x06000026 RID: 38 RVA: 0x00002690 File Offset: 0x00000890
    public void ConfigureGrid(int gridRows, int gridCols, float cellWidth, float cellHeight, float spacingX, float spacingY)
    {
        if (this.gridLayoutGroup == null)
        {
            Debug.LogError("GridLayoutGroup is not assigned!");
            return;
        }
        this.gridLayoutGroup.cellSize = new Vector2(cellWidth, cellHeight);
        this.gridLayoutGroup.spacing = new Vector2(spacingX, spacingY);
        float x = (float)gridCols * cellWidth + (float)(gridCols - 1) * spacingX;
        float y = (float)gridRows * cellHeight + (float)(gridRows - 1) * spacingY;
        this.cardParentRect.sizeDelta = new Vector2(x, y);
        this.gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        this.gridLayoutGroup.constraintCount = gridCols;
    }

    // Token: 0x0400001E RID: 30
    public GridLayoutGroup gridLayoutGroup;

    // Token: 0x0400001F RID: 31
    public int gridRows = 2;

    // Token: 0x04000020 RID: 32
    public int gridCols = 4;

    // Token: 0x04000021 RID: 33
    public float cellWidth = 150f;

    // Token: 0x04000022 RID: 34
    public float cellHeight = 150f;

    // Token: 0x04000023 RID: 35
    public float spacingX = 10f;

    // Token: 0x04000024 RID: 36
    public float spacingY = 10f;

    // Token: 0x04000025 RID: 37
    public RectTransform cardParentRect;
}

using System;
using UnityEngine;

// Token: 0x02000069 RID: 105
public class GunPickup : MonoBehaviour
{
    // Token: 0x06000217 RID: 535 RVA: 0x0000A335 File Offset: 0x00008535
    private void Start()
    {
        if (PlayerPrefs.GetString("Gun", "false") == "true")
        {
            base.gameObject.SetActive(false);
        }
    }

    // Token: 0x06000218 RID: 536 RVA: 0x0000A360 File Offset: 0x00008560
    public void PickupGun()
    {
        this.Gun.SetActive(true);
        this.Aim.SetActive(true);
        this.GunText.SetActive(true);
        if (this.Sound != null)
        {
            Object.Instantiate<GameObject>(this.Sound, base.transform.position, Quaternion.identity);
        }
        this.player.hasGun = "true";
        Object.Destroy(base.gameObject);
    }

    // Token: 0x04000284 RID: 644
    public GameObject Gun;

    // Token: 0x04000285 RID: 645
    public GameObject Aim;

    // Token: 0x04000286 RID: 646
    public GameObject GunText;

    // Token: 0x04000287 RID: 647
    public GameObject Sound;

    // Token: 0x04000288 RID: 648
    public PlayerController player;
}

using System;
using UnityEngine;

// Token: 0x0200000C RID: 12
public class HorrorSoundTrigger : MonoBehaviour
{
    // Token: 0x06000028 RID: 40 RVA: 0x00002771 File Offset: 0x00000971
    private void Start()
    {
    }

    // Token: 0x06000029 RID: 41 RVA: 0x00002773 File Offset: 0x00000973
    private void Update()
    {
    }
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

// Token: 0x02000031 RID: 49
public class Intro : MonoBehaviour
{
    // Token: 0x06000103 RID: 259 RVA: 0x00005908 File Offset: 0x00003B08
    private void Awake()
    {
        this.LoadIntro();
    }

    // Token: 0x06000104 RID: 260 RVA: 0x00005910 File Offset: 0x00003B10
    private void Start()
    {
        if (this.hasWatched == "false")
        {
            base.StartCoroutine(this.WatchVid());
            return;
        }
        this.videoClip.gameObject.SetActive(false);
    }

    // Token: 0x06000105 RID: 261 RVA: 0x00005943 File Offset: 0x00003B43
    private IEnumerator WatchVid()
    {
        PlayerController player = Object.FindObjectOfType<PlayerController>();
        player.isStopped = true;
        this.videoClip.gameObject.SetActive(true);
        this.videoClip.Play();
        yield return new WaitForSeconds(38.1f);
        this.hasWatched = "true";
        this.videoClip.gameObject.SetActive(false);
        player.isStopped = false;
        this.SaveIntro();
        yield break;
    }

    // Token: 0x06000106 RID: 262 RVA: 0x00005952 File Offset: 0x00003B52
    private void SaveIntro()
    {
        PlayerPrefs.SetString("Intro", this.hasWatched);
    }

    // Token: 0x06000107 RID: 263 RVA: 0x00005964 File Offset: 0x00003B64
    private void LoadIntro()
    {
        this.hasWatched = PlayerPrefs.GetString("Intro", "false");
    }

    // Token: 0x04000127 RID: 295
    public string hasWatched = "false";

    // Token: 0x04000128 RID: 296
    public VideoPlayer videoClip;
}

using System;
using UnityEngine;

// Token: 0x02000033 RID: 51
public class Item : MonoBehaviour
{
    // Token: 0x0600010C RID: 268 RVA: 0x0000599A File Offset: 0x00003B9A
    private void Start()
    {
        if (!this.isFinalMushroom)
        {
            this.LoadPresence();
        }
    }

    // Token: 0x0600010D RID: 269 RVA: 0x000059AC File Offset: 0x00003BAC
    public void OnCollect()
    {
        this.isCollected = "yes";
        if (this.pickSound != null)
        {
            Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
        }
        if (this.isFinalMushroom)
        {
            base.GetComponent<ShaiActivator>().OnPick();
        }
        base.gameObject.SetActive(false);
    }

    // Token: 0x0600010E RID: 270 RVA: 0x00005A0D File Offset: 0x00003C0D
    public void SavePresence()
    {
        PlayerPrefs.SetString("isCollected" + base.gameObject.name, this.isCollected);
    }

    // Token: 0x0600010F RID: 271 RVA: 0x00005A2F File Offset: 0x00003C2F
    public void LoadPresence()
    {
        this.isCollected = PlayerPrefs.GetString("isCollected" + base.gameObject.name);
        this.CheckPresence();
    }

    // Token: 0x06000110 RID: 272 RVA: 0x00005A57 File Offset: 0x00003C57
    public void CheckPresence()
    {
        if (this.isCollected == "yes")
        {
            base.gameObject.SetActive(false);
        }
    }

    // Token: 0x04000129 RID: 297
    public string isCollected;

    // Token: 0x0400012A RID: 298
    public GameObject pickSound;

    // Token: 0x0400012B RID: 299
    public int count = 1;

    // Token: 0x0400012C RID: 300
    public bool isFinalMushroom;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000034 RID: 52
public class ItemCollector : MonoBehaviour
{
    // Token: 0x06000112 RID: 274 RVA: 0x00005A86 File Offset: 0x00003C86
    private void Start()
    {
        this.LoadMushrooms();
    }

    // Token: 0x06000113 RID: 275 RVA: 0x00005A90 File Offset: 0x00003C90
    private void Update()
    {
        RaycastHit raycastHit;
        if (Input.GetKeyDown(KeyCode.E) && Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out raycastHit, this.pickupRange, this.itemMask))
        {
            Item item;
            if (raycastHit.collider.TryGetComponent<Item>(out item))
            {
                this.items += item.count;
                this.itemText.text = (this.items.ToString() ?? "");
                raycastHit.collider.GetComponent<Item>().OnCollect();
                Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                Action action = this.hasCollected;
                if (action == null)
                {
                    return;
                }
                action();
                return;
            }
            else
            {
                if (raycastHit.collider.CompareTag("Flower"))
                {
                    base.GetComponent<PlayerController>().moveSpeed = 8f;
                    Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                    Object.Destroy(raycastHit.collider.gameObject);
                    return;
                }
                if (raycastHit.collider.CompareTag("Wood"))
                {
                    Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                    this.itemText.transform.parent.gameObject.SetActive(true);
                    this.items++;
                    this.itemText.text = (this.items.ToString() ?? "");
                    Object.Destroy(raycastHit.collider.gameObject);
                    return;
                }
                GunPickup gunPickup;
                if (raycastHit.collider.TryGetComponent<GunPickup>(out gunPickup))
                {
                    Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                    gunPickup.PickupGun();
                    return;
                }
                MemoryGameTrigger memoryGameTrigger;
                if (raycastHit.collider.TryGetComponent<MemoryGameTrigger>(out memoryGameTrigger))
                {
                    memoryGameTrigger.ActivateGame();
                    return;
                }
                if (raycastHit.collider.GetComponent<EndGame>() != null)
                {
                    Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                    raycastHit.collider.GetComponent<EndGame>().FadeToTitles();
                }
            }
        }
    }

    // Token: 0x06000114 RID: 276 RVA: 0x00005CBA File Offset: 0x00003EBA
    public void SaveMushrooms()
    {
        PlayerPrefs.SetInt("mushrooms", this.items);
    }

    // Token: 0x06000115 RID: 277 RVA: 0x00005CCC File Offset: 0x00003ECC
    public void LoadMushrooms()
    {
        this.items = PlayerPrefs.GetInt("mushrooms", 0);
        this.itemText.text = (this.items.ToString() ?? "");
        Action action = this.hasCollected;
        if (action == null)
        {
            return;
        }
        action();
    }

    // Token: 0x0400012D RID: 301
    public Action hasCollected;

    // Token: 0x0400012E RID: 302
    public TextMeshProUGUI itemText;

    // Token: 0x0400012F RID: 303
    public LayerMask itemMask;

    // Token: 0x04000130 RID: 304
    public GameObject pickSound;

    // Token: 0x04000131 RID: 305
    public int items;

    // Token: 0x04000132 RID: 306
    public float pickupRange;
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Token: 0x0200000F RID: 15
public class Joystick : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IDragHandler, IPointerUpHandler
{
    // Token: 0x17000001 RID: 1
    // (get) Token: 0x06000033 RID: 51 RVA: 0x00002903 File Offset: 0x00000B03
    public float Horizontal
    {
        get
        {
            if (!this.snapX)
            {
                return this.input.x;
            }
            return this.SnapFloat(this.input.x, AxisOptions.Horizontal);
        }
    }

    // Token: 0x17000002 RID: 2
    // (get) Token: 0x06000034 RID: 52 RVA: 0x0000292B File Offset: 0x00000B2B
    public float Vertical
    {
        get
        {
            if (!this.snapY)
            {
                return this.input.y;
            }
            return this.SnapFloat(this.input.y, AxisOptions.Vertical);
        }
    }

    // Token: 0x17000003 RID: 3
    // (get) Token: 0x06000035 RID: 53 RVA: 0x00002953 File Offset: 0x00000B53
    public Vector2 Direction
    {
        get
        {
            return new Vector2(this.Horizontal, this.Vertical);
        }
    }

    // Token: 0x17000004 RID: 4
    // (get) Token: 0x06000036 RID: 54 RVA: 0x00002966 File Offset: 0x00000B66
    // (set) Token: 0x06000037 RID: 55 RVA: 0x0000296E File Offset: 0x00000B6E
    public float HandleRange
    {
        get
        {
            return this.handleRange;
        }
        set
        {
            this.handleRange = Mathf.Abs(value);
        }
    }

    // Token: 0x17000005 RID: 5
    // (get) Token: 0x06000038 RID: 56 RVA: 0x0000297C File Offset: 0x00000B7C
    // (set) Token: 0x06000039 RID: 57 RVA: 0x00002984 File Offset: 0x00000B84
    public float DeadZone
    {
        get
        {
            return this.deadZone;
        }
        set
        {
            this.deadZone = Mathf.Abs(value);
        }
    }

    // Token: 0x17000006 RID: 6
    // (get) Token: 0x0600003A RID: 58 RVA: 0x00002992 File Offset: 0x00000B92
    // (set) Token: 0x0600003B RID: 59 RVA: 0x0000299A File Offset: 0x00000B9A
    public AxisOptions AxisOptions
    {
        get
        {
            return this.AxisOptions;
        }
        set
        {
            this.axisOptions = value;
        }
    }

    // Token: 0x17000007 RID: 7
    // (get) Token: 0x0600003C RID: 60 RVA: 0x000029A3 File Offset: 0x00000BA3
    // (set) Token: 0x0600003D RID: 61 RVA: 0x000029AB File Offset: 0x00000BAB
    public bool SnapX
    {
        get
        {
            return this.snapX;
        }
        set
        {
            this.snapX = value;
        }
    }

    // Token: 0x17000008 RID: 8
    // (get) Token: 0x0600003E RID: 62 RVA: 0x000029B4 File Offset: 0x00000BB4
    // (set) Token: 0x0600003F RID: 63 RVA: 0x000029BC File Offset: 0x00000BBC
    public bool SnapY
    {
        get
        {
            return this.snapY;
        }
        set
        {
            this.snapY = value;
        }
    }

    // Token: 0x06000040 RID: 64 RVA: 0x000029C8 File Offset: 0x00000BC8
    protected virtual void Start()
    {
        this.HandleRange = this.handleRange;
        this.DeadZone = this.deadZone;
        this.baseRect = base.GetComponent<RectTransform>();
        this.canvas = base.GetComponentInParent<Canvas>();
        if (this.canvas == null)
        {
            Debug.LogError("The Joystick is not placed inside a canvas");
        }
        Vector2 vector = new Vector2(0.5f, 0.5f);
        this.background.pivot = vector;
        this.handle.anchorMin = vector;
        this.handle.anchorMax = vector;
        this.handle.pivot = vector;
        this.handle.anchoredPosition = Vector2.zero;
    }

    // Token: 0x06000041 RID: 65 RVA: 0x00002A6E File Offset: 0x00000C6E
    public virtual void OnPointerDown(PointerEventData eventData)
    {
        this.OnDrag(eventData);
    }

    // Token: 0x06000042 RID: 66 RVA: 0x00002A78 File Offset: 0x00000C78
    public void OnDrag(PointerEventData eventData)
    {
        this.cam = null;
        if (this.canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            this.cam = this.canvas.worldCamera;
        }
        Vector2 b = RectTransformUtility.WorldToScreenPoint(this.cam, this.background.position);
        Vector2 vector = this.background.sizeDelta / 2f;
        this.input = (eventData.position - b) / (vector * this.canvas.scaleFactor);
        this.FormatInput();
        this.HandleInput(this.input.magnitude, this.input.normalized, vector, this.cam);
        this.handle.anchoredPosition = this.input * vector * this.handleRange;
    }

    // Token: 0x06000043 RID: 67 RVA: 0x00002B4B File Offset: 0x00000D4B
    protected virtual void HandleInput(float magnitude, Vector2 normalised, Vector2 radius, Camera cam)
    {
        if (magnitude > this.deadZone)
        {
            if (magnitude > 1f)
            {
                this.input = normalised;
                return;
            }
        }
        else
        {
            this.input = Vector2.zero;
        }
    }

    // Token: 0x06000044 RID: 68 RVA: 0x00002B74 File Offset: 0x00000D74
    private void FormatInput()
    {
        if (this.axisOptions == AxisOptions.Horizontal)
        {
            this.input = new Vector2(this.input.x, 0f);
            return;
        }
        if (this.axisOptions == AxisOptions.Vertical)
        {
            this.input = new Vector2(0f, this.input.y);
        }
    }

    // Token: 0x06000045 RID: 69 RVA: 0x00002BCC File Offset: 0x00000DCC
    private float SnapFloat(float value, AxisOptions snapAxis)
    {
        if (value == 0f)
        {
            return value;
        }
        if (this.axisOptions == AxisOptions.Both)
        {
            float num = Vector2.Angle(this.input, Vector2.up);
            if (snapAxis == AxisOptions.Horizontal)
            {
                if (num < 22.5f || num > 157.5f)
                {
                    return 0f;
                }
                return (float)((value > 0f) ? 1 : -1);
            }
            else
            {
                if (snapAxis != AxisOptions.Vertical)
                {
                    return value;
                }
                if (num > 67.5f && num < 112.5f)
                {
                    return 0f;
                }
                return (float)((value > 0f) ? 1 : -1);
            }
        }
        else
        {
            if (value > 0f)
            {
                return 1f;
            }
            if (value < 0f)
            {
                return -1f;
            }
            return 0f;
        }
    }

    // Token: 0x06000046 RID: 70 RVA: 0x00002C6F File Offset: 0x00000E6F
    public virtual void OnPointerUp(PointerEventData eventData)
    {
        this.input = Vector2.zero;
        this.handle.anchoredPosition = Vector2.zero;
    }

    // Token: 0x06000047 RID: 71 RVA: 0x00002C8C File Offset: 0x00000E8C
    protected Vector2 ScreenPointToAnchoredPosition(Vector2 screenPosition)
    {
        Vector2 zero = Vector2.zero;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(this.baseRect, screenPosition, this.cam, out zero))
        {
            Vector2 b = this.baseRect.pivot * this.baseRect.sizeDelta;
            return zero - this.background.anchorMax * this.baseRect.sizeDelta + b;
        }
        return Vector2.zero;
    }

    // Token: 0x0400002D RID: 45
    [SerializeField]
    private float handleRange = 1f;

    // Token: 0x0400002E RID: 46
    [SerializeField]
    private float deadZone;

    // Token: 0x0400002F RID: 47
    [SerializeField]
    private AxisOptions axisOptions;

    // Token: 0x04000030 RID: 48
    [SerializeField]
    private bool snapX;

    // Token: 0x04000031 RID: 49
    [SerializeField]
    private bool snapY;

    // Token: 0x04000032 RID: 50
    [SerializeField]
    protected RectTransform background;

    // Token: 0x04000033 RID: 51
    [SerializeField]
    private RectTransform handle;

    // Token: 0x04000034 RID: 52
    private RectTransform baseRect;

    // Token: 0x04000035 RID: 53
    private Canvas canvas;

    // Token: 0x04000036 RID: 54
    private Camera cam;

    // Token: 0x04000037 RID: 55
    private Vector2 input = Vector2.zero;
}

using System;
using UnityEngine;

// Token: 0x0200000D RID: 13
public class JoystickPlayerExample : MonoBehaviour
{
    // Token: 0x0600002B RID: 43 RVA: 0x00002780 File Offset: 0x00000980
    public void FixedUpdate()
    {
        Vector3 a = Vector3.forward * this.variableJoystick.Vertical + Vector3.right * this.variableJoystick.Horizontal;
        this.rb.AddForce(a * this.speed * Time.fixedDeltaTime, ForceMode.VelocityChange);
    }

    // Token: 0x04000026 RID: 38
    public float speed;

    // Token: 0x04000027 RID: 39
    public VariableJoystick variableJoystick;

    // Token: 0x04000028 RID: 40
    public Rigidbody rb;
}

using System;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x0200000E RID: 14
public class JoystickSetterExample : MonoBehaviour
{
    // Token: 0x0600002D RID: 45 RVA: 0x000027E7 File Offset: 0x000009E7
    public void ModeChanged(int index)
    {
        switch (index)
        {
            case 0:
                this.variableJoystick.SetMode(JoystickType.Fixed);
                return;
            case 1:
                this.variableJoystick.SetMode(JoystickType.Floating);
                return;
            case 2:
                this.variableJoystick.SetMode(JoystickType.Dynamic);
                return;
            default:
                return;
        }
    }

    // Token: 0x0600002E RID: 46 RVA: 0x00002824 File Offset: 0x00000A24
    public void AxisChanged(int index)
    {
        switch (index)
        {
            case 0:
                this.variableJoystick.AxisOptions = AxisOptions.Both;
                this.background.sprite = this.axisSprites[index];
                return;
            case 1:
                this.variableJoystick.AxisOptions = AxisOptions.Horizontal;
                this.background.sprite = this.axisSprites[index];
                return;
            case 2:
                this.variableJoystick.AxisOptions = AxisOptions.Vertical;
                this.background.sprite = this.axisSprites[index];
                return;
            default:
                return;
        }
    }

    // Token: 0x0600002F RID: 47 RVA: 0x000028A3 File Offset: 0x00000AA3
    public void SnapX(bool value)
    {
        this.variableJoystick.SnapX = value;
    }

    // Token: 0x06000030 RID: 48 RVA: 0x000028B1 File Offset: 0x00000AB1
    public void SnapY(bool value)
    {
        this.variableJoystick.SnapY = value;
    }

    // Token: 0x06000031 RID: 49 RVA: 0x000028C0 File Offset: 0x00000AC0
    private void Update()
    {
        this.valueText.text = "Current Value: " + this.variableJoystick.Direction.ToString();
    }

    // Token: 0x04000029 RID: 41
    public VariableJoystick variableJoystick;

    // Token: 0x0400002A RID: 42
    public Text valueText;

    // Token: 0x0400002B RID: 43
    public Image background;

    // Token: 0x0400002C RID: 44
    public Sprite[] axisSprites;
}

using System;

// Token: 0x02000015 RID: 21
public enum JoystickType
{
    // Token: 0x04000041 RID: 65
    Fixed,
    // Token: 0x04000042 RID: 66
    Floating,
    // Token: 0x04000043 RID: 67
    Dynamic
}

using System;
using UnityEngine;

// Token: 0x02000032 RID: 50
public class LevelSwitcher : MonoBehaviour
{
    // Token: 0x06000109 RID: 265 RVA: 0x0000598E File Offset: 0x00003B8E
    private void Start()
    {
    }

    // Token: 0x0600010A RID: 266 RVA: 0x00005990 File Offset: 0x00003B90
    private void Update()
    {
    }
}

using System;
using UnityEngine;

// Token: 0x02000050 RID: 80
public class LightSwitcher : MonoBehaviour
{
    // Token: 0x060001A3 RID: 419 RVA: 0x0000832D File Offset: 0x0000652D
    private void Update()
    {
        if (Input.GetAxis("Mouse ScrollWheel") != 0f || Input.GetKeyDown(KeyCode.Q))
        {
            this.SwitchLight();
        }
    }

    // Token: 0x060001A4 RID: 420 RVA: 0x00008350 File Offset: 0x00006550
    private void SwitchLight()
    {
        if (!this.flashlight.activeInHierarchy)
        {
            if (this.oilLamp != null)
            {
                this.oilLamp.SetActive(false);
            }
            this.flashlight.SetActive(true);
            return;
        }
        if (this.flashlight.activeInHierarchy)
        {
            this.flashlight.SetActive(false);
            if (this.oilLamp != null)
            {
                this.oilLamp.SetActive(true);
            }
        }
    }

    // Token: 0x04000204 RID: 516
    public GameObject flashlight;

    // Token: 0x04000205 RID: 517
    public GameObject oilLamp;
}

using System;

// Token: 0x02000077 RID: 119
public enum LocalizationEnum
{
    // Token: 0x040002C1 RID: 705
    English,
    // Token: 0x040002C2 RID: 706
    Polish
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x0200003E RID: 62
public class LockUI : MonoBehaviour
{
    // Token: 0x0600014B RID: 331 RVA: 0x00006A89 File Offset: 0x00004C89
    private void Awake()
    {
        this.LoadPassword();
    }

    // Token: 0x0600014C RID: 332 RVA: 0x00006A91 File Offset: 0x00004C91
    public void SwitchPanelState(bool state)
    {
        this.lockPanel.SetActive(state);
    }

    // Token: 0x0600014D RID: 333 RVA: 0x00006AA0 File Offset: 0x00004CA0
    public void CheckPassword()
    {
        if (this.inputField.text.ToLower() == this.password)
        {
            this.checker.enabled = true;
            this.hintText.SetActive(false);
            this.inputField.gameObject.SetActive(false);
            this.rewardMessage.SetActive(true);
            base.gameObject.SetActive(false);
            this.enteredPassword = this.password;
            this.SavePassword();
        }
    }

    // Token: 0x0600014E RID: 334 RVA: 0x00006B1D File Offset: 0x00004D1D
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.lockPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
        }
    }

    // Token: 0x0600014F RID: 335 RVA: 0x00006B4E File Offset: 0x00004D4E
    public void OnFoodKilled()
    {
        this.foodKilled = "true";
        this.SavePassword();
        this.Activate();
    }

    // Token: 0x06000150 RID: 336 RVA: 0x00006B68 File Offset: 0x00004D68
    private void LoadPassword()
    {
        this.enteredPassword = PlayerPrefs.GetString("Password", "");
        this.foodKilled = PlayerPrefs.GetString("FoodKilled", "");
        if (this.enteredPassword == "offscreen")
        {
            this.checker.enabled = true;
            base.gameObject.SetActive(false);
            return;
        }
        if (this.foodKilled == "true")
        {
            this.Activate();
        }
    }

    // Token: 0x06000151 RID: 337 RVA: 0x00006BE2 File Offset: 0x00004DE2
    private void SavePassword()
    {
        PlayerPrefs.SetString("Password", this.enteredPassword);
        PlayerPrefs.SetString("FoodKilled", this.foodKilled);
    }

    // Token: 0x06000152 RID: 338 RVA: 0x00006C04 File Offset: 0x00004E04
    public void Activate()
    {
        base.GetComponent<Outline>().enabled = true;
        base.GetComponent<SphereCollider>().enabled = true;
    }

    // Token: 0x0400018F RID: 399
    [SerializeField]
    private GameObject lockPanel;

    // Token: 0x04000190 RID: 400
    [SerializeField]
    private GameObject rewardMessage;

    // Token: 0x04000191 RID: 401
    [SerializeField]
    private GameObject hintText;

    // Token: 0x04000192 RID: 402
    [SerializeField]
    private TMP_InputField inputField;

    // Token: 0x04000193 RID: 403
    [SerializeField]
    private MushroomChecker checker;

    // Token: 0x04000194 RID: 404
    [SerializeField]
    private string password;

    // Token: 0x04000195 RID: 405
    private string foodKilled = "";

    // Token: 0x04000196 RID: 406
    private string enteredPassword = "";
}

using System;
using UnityEngine;

// Token: 0x02000016 RID: 22
public class Melon : MonoBehaviour
{
    // Token: 0x0600005D RID: 93 RVA: 0x00002FC4 File Offset: 0x000011C4
    private void Start()
    {
        this._rigidbody = base.GetComponent<Rigidbody>();
    }

    // Token: 0x0600005E RID: 94 RVA: 0x00002FD2 File Offset: 0x000011D2
    private void FixedUpdate()
    {
        this._rigidbody.AddForce(base.transform.forward * this.moveSpeed, ForceMode.Force);
    }

    // Token: 0x0600005F RID: 95 RVA: 0x00002FF6 File Offset: 0x000011F6
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            this.DestroyMelon();
        }
    }

    // Token: 0x06000060 RID: 96 RVA: 0x0000301A File Offset: 0x0000121A
    public void DestroyMelon()
    {
        Object.Instantiate<GameObject>(this.destroyEffect, base.transform.position, Quaternion.identity);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x06000061 RID: 97 RVA: 0x00003044 File Offset: 0x00001244
    private void OnCollisionEnter(Collision other)
    {
        PlayerHealth playerHealth;
        if (other.gameObject.CompareTag("Player") && other.gameObject.TryGetComponent<PlayerHealth>(out playerHealth))
        {
            playerHealth.TakeDamage(this.damage);
            this.DestroyMelon();
        }
    }

    // Token: 0x04000044 RID: 68
    public float moveSpeed;

    // Token: 0x04000045 RID: 69
    public float health;

    // Token: 0x04000046 RID: 70
    public float damage;

    // Token: 0x04000047 RID: 71
    public GameObject destroyEffect;

    // Token: 0x04000048 RID: 72
    private Rigidbody _rigidbody;
}

using System;
using UnityEngine;

// Token: 0x02000017 RID: 23
public class MelonExitTrigger : MonoBehaviour
{
    // Token: 0x06000063 RID: 99 RVA: 0x0000308C File Offset: 0x0000128C
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.hitboxes.SetActive(false);
            this.cylinder.SetActive(true);
        }
    }

    // Token: 0x04000049 RID: 73
    public GameObject hitboxes;

    // Token: 0x0400004A RID: 74
    public GameObject cylinder;
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Token: 0x02000018 RID: 24
public class MelonfallManager : MonoBehaviour
{
    // Token: 0x1700000B RID: 11
    // (get) Token: 0x06000065 RID: 101 RVA: 0x000030BB File Offset: 0x000012BB
    private float startSpawnTime
    {
        get
        {
            return this.decreaseCurve.Evaluate(this._time);
        }
    }

    // Token: 0x06000066 RID: 102 RVA: 0x000030CE File Offset: 0x000012CE
    private void Start()
    {
        this._animator = base.GetComponent<Animator>();
    }

    // Token: 0x06000067 RID: 103 RVA: 0x000030DC File Offset: 0x000012DC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.StartGame();
            base.GetComponent<Collider>().enabled = false;
        }
    }

    // Token: 0x06000068 RID: 104 RVA: 0x000030FD File Offset: 0x000012FD
    public void StartGame()
    {
        this._isInGame = true;
        this.timerText.gameObject.SetActive(true);
        MusicManager.instance.EnableMusic(2);
        this._animator.SetTrigger("hasEntered");
    }

    // Token: 0x06000069 RID: 105 RVA: 0x00003134 File Offset: 0x00001334
    private void Update()
    {
        if (this._isInGame)
        {
            this.showTime = this.startSpawnTime;
            this._spawnTime -= Time.deltaTime;
            this._time += Time.deltaTime;
            float num = (float)Mathf.FloorToInt((60f - this._time) / 60f);
            float num2 = (float)Mathf.FloorToInt((60f - this._time) % 60f);
            this.timerText.text = string.Format("{0:00}:{1:00}", num, num2);
            if (this._spawnTime <= 0f)
            {
                if (this.startSpawnTime < 1.3f)
                {
                    this.SpawnMelons(3);
                }
                else if (this.startSpawnTime < 2f)
                {
                    this.SpawnMelons(2);
                }
                else
                {
                    this.SpawnMelons(1);
                }
                this._spawnTime = this.startSpawnTime;
            }
            if (this._time >= this.totalGameTime)
            {
                this._isInGame = false;
                this.timerText.gameObject.SetActive(false);
                Melon[] array = Object.FindObjectsOfType<Melon>();
                for (int i = 0; i < array.Length; i++)
                {
                    array[i].DestroyMelon();
                }
                Object.Instantiate<GameObject>(this.melonfallOverSound, base.transform.position, Quaternion.identity);
                this._animator.SetBool("isCompleted", true);
                Action action = this.hasCompletedMelonfall;
                if (action == null)
                {
                    return;
                }
                action();
            }
        }
    }

    // Token: 0x0600006A RID: 106 RVA: 0x0000329C File Offset: 0x0000149C
    public void SpawnMelons(int numberOfMelons)
    {
        if (numberOfMelons > this.spawnPositions.Length)
        {
            Debug.LogWarning("Number of melons to spawn exceeds available spawn positions.");
            numberOfMelons = this.spawnPositions.Length;
        }
        List<int> list = new List<int>();
        for (int i = 0; i < this.spawnPositions.Length; i++)
        {
            list.Add(i);
        }
        this.ShuffleList(list);
        for (int j = 0; j < numberOfMelons; j++)
        {
            int num = list[j];
            Melon component;
            if (numberOfMelons >= 3 && j == 0)
            {
                component = Object.Instantiate<GameObject>(this.melonShootable, this.spawnPositions[num].position, this.spawnPositions[num].rotation).GetComponent<Melon>();
            }
            else
            {
                component = Object.Instantiate<GameObject>(this.melon, this.spawnPositions[num].position, this.spawnPositions[num].rotation).GetComponent<Melon>();
            }
            component.moveSpeed = 1f / (this.startSpawnTime / 1.3f);
        }
    }

    // Token: 0x0600006B RID: 107 RVA: 0x00003388 File Offset: 0x00001588
    private void ShuffleList(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int index = Random.Range(i, list.Count);
            int value = list[i];
            list[i] = list[index];
            list[index] = value;
        }
    }

    // Token: 0x0400004B RID: 75
    [SerializeField]
    private Transform[] spawnPositions;

    // Token: 0x0400004C RID: 76
    [SerializeField]
    private GameObject melon;

    // Token: 0x0400004D RID: 77
    [SerializeField]
    private GameObject melonShootable;

    // Token: 0x0400004E RID: 78
    [SerializeField]
    private GameObject melonfallOverSound;

    // Token: 0x0400004F RID: 79
    [SerializeField]
    private AnimationCurve decreaseCurve;

    // Token: 0x04000050 RID: 80
    [SerializeField]
    private TextMeshProUGUI timerText;

    // Token: 0x04000051 RID: 81
    private Animator _animator;

    // Token: 0x04000052 RID: 82
    public Action hasCompletedMelonfall;

    // Token: 0x04000053 RID: 83
    public float totalGameTime;

    // Token: 0x04000054 RID: 84
    public float showTime;

    // Token: 0x04000055 RID: 85
    private float _time;

    // Token: 0x04000056 RID: 86
    private float _spawnTime;

    // Token: 0x04000057 RID: 87
    private bool _isInGame;
}

using System;
using UnityEngine;

// Token: 0x02000019 RID: 25
public class MemoryGameTrigger : MonoBehaviour
{
    // Token: 0x0600006D RID: 109 RVA: 0x000033D9 File Offset: 0x000015D9
    private void Start()
    {
        this.spriteRenderer = base.GetComponent<SpriteRenderer>();
    }

    // Token: 0x0600006E RID: 110 RVA: 0x000033E7 File Offset: 0x000015E7
    public void ActivateGame()
    {
        this.spriteRenderer.sprite = this.inGameSprite;
        this.memoryGame.SetActive(true);
    }

    // Token: 0x0600006F RID: 111 RVA: 0x00003406 File Offset: 0x00001606
    public void CloseGame()
    {
        this.spriteRenderer.sprite = this.interruptGameSprite;
        this.memoryGame.SetActive(false);
    }

    // Token: 0x06000070 RID: 112 RVA: 0x00003428 File Offset: 0x00001628
    public void OnGameOver()
    {
        base.GetComponent<Collider>().enabled = false;
        this.memoryGame.SetActive(false);
        this.spriteRenderer.sprite = this.gameOverSprite;
        this.thingsToTeleport.SetActive(true);
        this.player.transform.parent = this.thingsToTeleport.transform;
        this.thingsToTeleport.transform.position = this.teleportDestinationPoint.position;
        this.thingsToTeleport.transform.rotation = this.teleportDestinationPoint.rotation;
        this.player.transform.parent = null;
        this.player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        this.playerCamera.FlipPlayer();
    }

    // Token: 0x04000058 RID: 88
    [SerializeField]
    private GameObject memoryGame;

    // Token: 0x04000059 RID: 89
    [SerializeField]
    private GameObject thingsToTeleport;

    // Token: 0x0400005A RID: 90
    [SerializeField]
    private Transform player;

    // Token: 0x0400005B RID: 91
    [SerializeField]
    private PlayerCamera playerCamera;

    // Token: 0x0400005C RID: 92
    [SerializeField]
    private Transform teleportDestinationPoint;

    // Token: 0x0400005D RID: 93
    [SerializeField]
    private Sprite interruptGameSprite;

    // Token: 0x0400005E RID: 94
    [SerializeField]
    private Sprite inGameSprite;

    // Token: 0x0400005F RID: 95
    [SerializeField]
    private Sprite gameOverSprite;

    // Token: 0x04000060 RID: 96
    private SpriteRenderer spriteRenderer;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Token: 0x0200005F RID: 95
public class Menu : MonoBehaviour
{
    // Token: 0x060001E7 RID: 487 RVA: 0x00009225 File Offset: 0x00007425
    private void Start()
    {
        this.sceneIndex = PlayerPrefs.GetInt("Scene", 1);
    }

    // Token: 0x060001E8 RID: 488 RVA: 0x00009238 File Offset: 0x00007438
    public void LoadGame()
    {
        this.sceneIndex = PlayerPrefs.GetInt("Scene", 1);
        base.StartCoroutine(this.LoadScene_Coroutine(this.sceneIndex));
    }

    // Token: 0x060001E9 RID: 489 RVA: 0x0000925E File Offset: 0x0000745E
    public void LoadNewGame()
    {
        PlayerPrefs.DeleteAll();
        this.LoadGame();
    }

    // Token: 0x060001EA RID: 490 RVA: 0x0000926B File Offset: 0x0000746B
    public IEnumerator LoadScene_Coroutine(int index)
    {
        this.progressSlider.value = 0f;
        this.LoaderUI.SetActive(true);
        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(index);
        asyncOperation.allowSceneActivation = false;
        float progress = 0f;
        while (!asyncOperation.isDone)
        {
            progress = Mathf.MoveTowards(progress, asyncOperation.progress, Time.deltaTime);
            this.progressSlider.value = progress;
            if (progress >= 0.9f)
            {
                this.progressSlider.value = 1f;
                asyncOperation.allowSceneActivation = true;
            }
            yield return null;
        }
        yield break;
    }

    // Token: 0x0400025A RID: 602
    public int sceneIndex;

    // Token: 0x0400025B RID: 603
    public GameObject LoaderUI;

    // Token: 0x0400025C RID: 604
    public Slider progressSlider;
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Token: 0x02000060 RID: 96
public class MenuExit : MonoBehaviour
{
    // Token: 0x060001EC RID: 492 RVA: 0x0000928C File Offset: 0x0000748C
    public void OnExit()
    {
        Time.timeScale = 1f;
        MusicManager musicManager = Object.FindObjectOfType<MusicManager>();
        if (musicManager.audioSource.clip != musicManager.musicVariants[0])
        {
            musicManager.EnableMusic(0);
        }
        this.SaveItemPresence();
        SceneManager.LoadScene("Menu");
    }

    // Token: 0x060001ED RID: 493 RVA: 0x000092DC File Offset: 0x000074DC
    public void SaveItemPresence()
    {
        Item[] array = this.items;
        for (int i = 0; i < array.Length; i++)
        {
            array[i].SavePresence();
        }
    }

    // Token: 0x0400025D RID: 605
    public Item[] items;
}

using System;
using UnityEngine;

// Token: 0x0200001A RID: 26
public class MoonGravityTrigger : MonoBehaviour
{
    // Token: 0x06000072 RID: 114 RVA: 0x00003503 File Offset: 0x00001703
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !this._hasTriggered)
        {
            this._hasTriggered = true;
            if (this.isMoon)
            {
                GravityController.instance.SetMoonGravity();
                return;
            }
            GravityController.instance.SetEarthGravity();
        }
    }

    // Token: 0x04000061 RID: 97
    public bool isMoon;

    // Token: 0x04000062 RID: 98
    private bool _hasTriggered;
}

using System;
using UnityEngine;

// Token: 0x0200007B RID: 123
public class MouseLook : MonoBehaviour
{
    // Token: 0x06000245 RID: 581 RVA: 0x0000AEC0 File Offset: 0x000090C0
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Token: 0x06000246 RID: 582 RVA: 0x0000AEC8 File Offset: 0x000090C8
    private void Update()
    {
        float d = Input.GetAxis("Mouse X") * this.mouseSensitivity * Time.deltaTime;
        float num = Input.GetAxis("Mouse Y") * this.mouseSensitivity * Time.deltaTime;
        this.xRotation -= num;
        this.xRotation = Mathf.Clamp(this.xRotation, -90f, 90f);
        base.transform.localRotation = Quaternion.Euler(this.xRotation, 0f, 0f);
        this.playerBody.Rotate(Vector3.up * d);
    }

    // Token: 0x040002CE RID: 718
    public float mouseSensitivity = 100f;

    // Token: 0x040002CF RID: 719
    public Transform playerBody;

    // Token: 0x040002D0 RID: 720
    private float xRotation;
}

using System;
using UnityEngine;

// Token: 0x02000051 RID: 81
public class MoveCamera : MonoBehaviour
{
    // Token: 0x060001A6 RID: 422 RVA: 0x000083CC File Offset: 0x000065CC
    private void FixedUpdate()
    {
        if (this.currentMode == MoveCamera.UpdateMode.FixedUpdate)
        {
            this.ApplyMovement();
        }
    }

    // Token: 0x060001A7 RID: 423 RVA: 0x000083DD File Offset: 0x000065DD
    private void Update()
    {
        if (this.currentMode == MoveCamera.UpdateMode.Update)
        {
            this.ApplyMovement();
        }
    }

    // Token: 0x060001A8 RID: 424 RVA: 0x000083EE File Offset: 0x000065EE
    private void LateUpdate()
    {
        if (this.currentMode == MoveCamera.UpdateMode.LateUpdate)
        {
            this.ApplyMovement();
        }
    }

    // Token: 0x060001A9 RID: 425 RVA: 0x000083FE File Offset: 0x000065FE
    private void ApplyMovement()
    {
        base.transform.position = this.cameraPosition.position;
    }

    // Token: 0x04000206 RID: 518
    public Transform cameraPosition;

    // Token: 0x04000207 RID: 519
    public MoveCamera.UpdateMode currentMode;

    // Token: 0x020000D0 RID: 208
    public enum UpdateMode
    {
        // Token: 0x0400047B RID: 1147
        LateUpdate,
        // Token: 0x0400047C RID: 1148
        Update,
        // Token: 0x0400047D RID: 1149
        FixedUpdate
    }
}

using System;
using UnityEngine;

// Token: 0x02000052 RID: 82
public class MoveForward : MonoBehaviour
{
    // Token: 0x060001AB RID: 427 RVA: 0x0000841E File Offset: 0x0000661E
    private void Update()
    {
        base.transform.Translate(Vector3.forward * this.speed * Time.deltaTime);
    }

    // Token: 0x04000208 RID: 520
    public float speed;
}

using System;
using UnityEngine;

// Token: 0x02000035 RID: 53
public class MushroomChecker : MonoBehaviour
{
    // Token: 0x06000117 RID: 279 RVA: 0x00005D21 File Offset: 0x00003F21
    private void Start()
    {
        this.collector = Object.FindObjectOfType<ItemCollector>();
    }

    // Token: 0x06000118 RID: 280 RVA: 0x00005D30 File Offset: 0x00003F30
    private void Update()
    {
        this.playerNearGate = Physics.CheckSphere(this.openingPoint.position, this.sightRange, this.whatIsPlayer);
        this.playerPastGate = Physics.CheckSphere(this.closingPoint.position, this.sightRange, this.whatIsPlayer);
        if (this.playerNearGate)
        {
            this.CheckForMushrooms();
        }
        if (this.playerPastGate)
        {
            this.CloseGate();
        }
    }

    // Token: 0x06000119 RID: 281 RVA: 0x00005DA8 File Offset: 0x00003FA8
    public void CheckForMushrooms()
    {
        if (this.collector.items >= this.mushroomsRequired && this.sable.activeInHierarchy)
        {
            this.gateAnimator.SetBool("hasMushrooms", true);
        }
        if (!this.alreadySaved)
        {
            this.alreadySaved = true;
            this.collector.SaveMushrooms();
            this.collector.GetComponent<PlayerController>().SavePosition();
            Object.FindObjectOfType<MenuExit>(true).SaveItemPresence();
        }
    }

    // Token: 0x0600011A RID: 282 RVA: 0x00005E1B File Offset: 0x0000401B
    public void CloseGate()
    {
        this.gateAnimator.SetBool("pastGate", true);
        base.enabled = false;
    }

    // Token: 0x0600011B RID: 283 RVA: 0x00005E38 File Offset: 0x00004038
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(this.openingPoint.position, this.sightRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(this.closingPoint.position, this.sightRange);
    }

    // Token: 0x04000133 RID: 307
    public GameObject sable;

    // Token: 0x04000134 RID: 308
    public Transform openingPoint;

    // Token: 0x04000135 RID: 309
    public Transform closingPoint;

    // Token: 0x04000136 RID: 310
    public Animator gateAnimator;

    // Token: 0x04000137 RID: 311
    public LayerMask whatIsPlayer;

    // Token: 0x04000138 RID: 312
    public float sightRange;

    // Token: 0x04000139 RID: 313
    public int mushroomsRequired;

    // Token: 0x0400013A RID: 314
    private ItemCollector collector;

    // Token: 0x0400013B RID: 315
    private bool playerNearGate;

    // Token: 0x0400013C RID: 316
    private bool playerPastGate;

    // Token: 0x0400013D RID: 317
    private bool alreadySaved;
}

using System;
using UnityEngine;

// Token: 0x0200003F RID: 63
public class MushroomDamage : MonoBehaviour
{
    // Token: 0x06000154 RID: 340 RVA: 0x00006C3C File Offset: 0x00004E3C
    private void Start()
    {
        this.shailushay = Object.FindObjectOfType<Shailushay>();
    }

    // Token: 0x06000155 RID: 341 RVA: 0x00006C49 File Offset: 0x00004E49
    private void OnDestroy()
    {
        if (this.shailushay != null)
        {
            this.shailushay.TakeDamage(this.damage);
        }
    }

    // Token: 0x04000197 RID: 407
    [SerializeField]
    private int damage;

    // Token: 0x04000198 RID: 408
    private Shailushay shailushay;
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Token: 0x02000036 RID: 54
public class MushroomGame : MonoBehaviour
{
    // Token: 0x0600011D RID: 285 RVA: 0x00005E8D File Offset: 0x0000408D
    private void OnTriggerEnter(Collider other)
    {
        if (!this.isReady && other.transform.root.CompareTag("Player"))
        {
            this.StartGame();
        }
    }

    // Token: 0x0600011E RID: 286 RVA: 0x00005EB4 File Offset: 0x000040B4
    private void StartGame()
    {
        this.gameTime = this.startGameTime;
        this.timerText.gameObject.SetActive(true);
        this.isReady = true;
        base.GetComponent<SphereCollider>().enabled = false;
        Object.FindObjectOfType<MusicManager>().EnableMusic(1);
    }

    // Token: 0x0600011F RID: 287 RVA: 0x00005EF4 File Offset: 0x000040F4
    private void Update()
    {
        if (this.isReady)
        {
            if (this.gameTime > 0f)
            {
                float num = (float)Mathf.FloorToInt(this.gameTime / 60f);
                float num2 = (float)Mathf.FloorToInt(this.gameTime % 60f);
                this.timerText.text = string.Format("{0:00}:{1:00}", num, num2);
                this.gameTime -= Time.deltaTime;
                if (this.timeBetweenSpawn > 0f)
                {
                    this.timeBetweenSpawn -= Time.deltaTime;
                    return;
                }
                int num3 = Random.Range(0, this.enemies.Length);
                int num4 = Random.Range(0, this.spawnPoints.Length);
                Object.Instantiate<GameObject>(this.enemies[num3], this.spawnPoints[num4].position, Quaternion.identity);
                this.timeBetweenSpawn = this.startTimeBetweenSpawn;
                if (this.startTimeBetweenSpawn > this.minTime)
                {
                    this.startTimeBetweenSpawn -= this.decreaseTime;
                    return;
                }
            }
            else if (!this.alreadyStarted)
            {
                this.alreadyStarted = true;
                base.StartCoroutine(this.StartBossfight());
            }
        }
    }

    // Token: 0x06000120 RID: 288 RVA: 0x0000601D File Offset: 0x0000421D
    public IEnumerator StartBossfight()
    {
        MushroomHunter[] array = Object.FindObjectsOfType<MushroomHunter>();
        for (int i = 0; i < array.Length; i++)
        {
            array[i].TakeDamage(1000);
        }
        this.timerText.gameObject.SetActive(false);
        this.mushroomAnimator.SetBool("gameOver", true);
        yield return new WaitForSeconds(2f);
        this.player.transform.position = this.posBeforeBossfight.position;
        this.player.transform.rotation = this.posBeforeBossfight.rotation;
        this.player.enabled = false;
        yield return new WaitForSeconds(2f);
        this.fenceAnimator.SetBool("bossfightStarted", true);
        yield return new WaitForSeconds(4f);
        this.bossHP.SetActive(true);
        Object.Instantiate<GameObject>(this.boss, this.bossSpawnPos.position, Quaternion.identity);
        this.player.enabled = true;
        base.enabled = false;
        yield break;
    }

    // Token: 0x0400013E RID: 318
    public GameObject[] enemies;

    // Token: 0x0400013F RID: 319
    public Transform[] spawnPoints;

    // Token: 0x04000140 RID: 320
    public TextMeshProUGUI timerText;

    // Token: 0x04000141 RID: 321
    public PlayerController player;

    // Token: 0x04000142 RID: 322
    public Animator mushroomAnimator;

    // Token: 0x04000143 RID: 323
    public Animator fenceAnimator;

    // Token: 0x04000144 RID: 324
    public GameObject boss;

    // Token: 0x04000145 RID: 325
    public GameObject bossHP;

    // Token: 0x04000146 RID: 326
    public Transform posBeforeBossfight;

    // Token: 0x04000147 RID: 327
    public Transform bossSpawnPos;

    // Token: 0x04000148 RID: 328
    private float gameTime;

    // Token: 0x04000149 RID: 329
    public float startGameTime;

    // Token: 0x0400014A RID: 330
    private float timeBetweenSpawn;

    // Token: 0x0400014B RID: 331
    public float startTimeBetweenSpawn;

    // Token: 0x0400014C RID: 332
    public float decreaseTime;

    // Token: 0x0400014D RID: 333
    public float minTime;

    // Token: 0x0400014E RID: 334
    private bool isReady;

    // Token: 0x0400014F RID: 335
    private bool alreadyStarted;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x02000037 RID: 55
public class MushroomHunter : MonoBehaviour
{
    // Token: 0x06000122 RID: 290 RVA: 0x00006034 File Offset: 0x00004234
    private void Awake()
    {
        this.agent = base.GetComponent<NavMeshAgent>();
    }

    // Token: 0x06000123 RID: 291 RVA: 0x00006042 File Offset: 0x00004242
    private void Start()
    {
        this.walkPoint = base.transform.position;
    }

    // Token: 0x06000124 RID: 292 RVA: 0x00006058 File Offset: 0x00004258
    private void Update()
    {
        this.FindClosestMushroom();
        this.mushroomInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsMushroom);
        this.mushroomInPickRange = Physics.CheckSphere(base.transform.position, this.pickRange, this.whatIsMushroom);
        if (!this.hasPicked)
        {
            if (!this.mushroomInSightRange && !this.mushroomInPickRange)
            {
                this.Patroling();
            }
            if (this.mushroomInSightRange && !this.mushroomInPickRange)
            {
                this.ChaseMushroom();
            }
            if (this.mushroomInPickRange && this.mushroomInSightRange)
            {
                this.PickMushroom();
                return;
            }
        }
        else
        {
            this.Patroling();
        }
    }

    // Token: 0x06000125 RID: 293 RVA: 0x0000610C File Offset: 0x0000430C
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 5f)
        {
            this.walkPointSet = false;
            if (this.hasPicked)
            {
                this.hasPicked = false;
                this.DestroyEnemy();
            }
        }
    }

    // Token: 0x06000126 RID: 294 RVA: 0x00006184 File Offset: 0x00004384
    public void FindClosestMushroom()
    {
        float num = float.PositiveInfinity;
        foreach (GameObject gameObject in GameObject.FindGameObjectsWithTag("CentralMushroom"))
        {
            float sqrMagnitude = (gameObject.transform.position - base.transform.position).sqrMagnitude;
            if (sqrMagnitude < num)
            {
                num = sqrMagnitude;
                this.closestMushroom = gameObject.transform;
            }
        }
    }

    // Token: 0x06000127 RID: 295 RVA: 0x000061EE File Offset: 0x000043EE
    private void SearchWalkPoint()
    {
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x06000128 RID: 296 RVA: 0x00006224 File Offset: 0x00004424
    private void ChaseMushroom()
    {
        this.agent.SetDestination(this.closestMushroom.position);
    }

    // Token: 0x06000129 RID: 297 RVA: 0x00006240 File Offset: 0x00004440
    private void PickMushroom()
    {
        this.hasPicked = true;
        this.closestMushroom.transform.parent = this.mushroomPos;
        this.closestMushroom.transform.position = this.mushroomPos.position;
        this.closestMushroom.transform.rotation = this.mushroomPos.rotation;
    }

    // Token: 0x0600012A RID: 298 RVA: 0x000062A0 File Offset: 0x000044A0
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            this.DestroyEnemy();
        }
    }

    // Token: 0x0600012B RID: 299 RVA: 0x000062C4 File Offset: 0x000044C4
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        if (this.hasPicked)
        {
            Object.Instantiate<GameObject>(this.newMushroom, base.transform.position, Quaternion.identity);
        }
        Object.Destroy(base.gameObject);
    }

    // Token: 0x0600012C RID: 300 RVA: 0x0000631C File Offset: 0x0000451C
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.pickRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x04000150 RID: 336
    public NavMeshAgent agent;

    // Token: 0x04000151 RID: 337
    public Transform mushroomPos;

    // Token: 0x04000152 RID: 338
    public Transform closestMushroom;

    // Token: 0x04000153 RID: 339
    public LayerMask whatIsGround;

    // Token: 0x04000154 RID: 340
    public LayerMask whatIsMushroom;

    // Token: 0x04000155 RID: 341
    public Vector3 walkPoint;

    // Token: 0x04000156 RID: 342
    private bool walkPointSet;

    // Token: 0x04000157 RID: 343
    public float walkPointRange;

    // Token: 0x04000158 RID: 344
    public float sightRange;

    // Token: 0x04000159 RID: 345
    public float pickRange;

    // Token: 0x0400015A RID: 346
    public bool mushroomInSightRange;

    // Token: 0x0400015B RID: 347
    public bool mushroomInPickRange;

    // Token: 0x0400015C RID: 348
    public bool hasPicked;

    // Token: 0x0400015D RID: 349
    public float health;

    // Token: 0x0400015E RID: 350
    public int damage;

    // Token: 0x0400015F RID: 351
    public GameObject deathEffect;

    // Token: 0x04000160 RID: 352
    public GameObject newMushroom;
}

using System;
using UnityEngine;

// Token: 0x0200005A RID: 90
public class MusicManager : MonoBehaviour
{
    // Token: 0x060001CF RID: 463 RVA: 0x00008DD1 File Offset: 0x00006FD1
    private void Awake()
    {
        if (MusicManager.instance != null)
        {
            Object.Destroy(base.gameObject);
            return;
        }
        MusicManager.instance = this;
        Object.DontDestroyOnLoad(base.transform.gameObject);
    }

    // Token: 0x060001D0 RID: 464 RVA: 0x00008E02 File Offset: 0x00007002
    private void Start()
    {
        this.audioSource.clip = this.musicVariants[0];
        this.audioSource.Play();
    }

    // Token: 0x060001D1 RID: 465 RVA: 0x00008E24 File Offset: 0x00007024
    public void EnableMusic(int musicIndex)
    {
        if (this.audioSource.clip != this.musicVariants[musicIndex])
        {
            this.audioSource.clip = this.musicVariants[musicIndex];
            this.audioSource.volume = this.volumes[musicIndex];
            this.audioSource.Play();
        }
    }

    // Token: 0x0400023F RID: 575
    public static MusicManager instance;

    // Token: 0x04000240 RID: 576
    public AudioSource audioSource;

    // Token: 0x04000241 RID: 577
    public AudioClip[] musicVariants;

    // Token: 0x04000242 RID: 578
    [Range(0f, 1f)]
    public float[] volumes;
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Token: 0x0200007D RID: 125
[DisallowMultipleComponent]
public class Outline : MonoBehaviour
{
    // Token: 0x17000011 RID: 17
    // (get) Token: 0x0600024A RID: 586 RVA: 0x0000B104 File Offset: 0x00009304
    // (set) Token: 0x0600024B RID: 587 RVA: 0x0000B10C File Offset: 0x0000930C
    public Outline.Mode OutlineMode
    {
        get
        {
            return this.outlineMode;
        }
        set
        {
            this.outlineMode = value;
            this.needsUpdate = true;
        }
    }

    // Token: 0x17000012 RID: 18
    // (get) Token: 0x0600024C RID: 588 RVA: 0x0000B11C File Offset: 0x0000931C
    // (set) Token: 0x0600024D RID: 589 RVA: 0x0000B124 File Offset: 0x00009324
    public Color OutlineColor
    {
        get
        {
            return this.outlineColor;
        }
        set
        {
            this.outlineColor = value;
            this.needsUpdate = true;
        }
    }

    // Token: 0x17000013 RID: 19
    // (get) Token: 0x0600024E RID: 590 RVA: 0x0000B134 File Offset: 0x00009334
    // (set) Token: 0x0600024F RID: 591 RVA: 0x0000B13C File Offset: 0x0000933C
    public float OutlineWidth
    {
        get
        {
            return this.outlineWidth;
        }
        set
        {
            this.outlineWidth = value;
            this.needsUpdate = true;
        }
    }

    // Token: 0x06000250 RID: 592 RVA: 0x0000B14C File Offset: 0x0000934C
    private void Awake()
    {
        this.renderers = base.GetComponentsInChildren<Renderer>();
        this.outlineMaskMaterial = Object.Instantiate<Material>(Resources.Load<Material>("Materials/OutlineMask"));
        this.outlineFillMaterial = Object.Instantiate<Material>(Resources.Load<Material>("Materials/OutlineFill"));
        this.outlineMaskMaterial.name = "OutlineMask (Instance)";
        this.outlineFillMaterial.name = "OutlineFill (Instance)";
        this.LoadSmoothNormals();
        this.needsUpdate = true;
    }

    // Token: 0x06000251 RID: 593 RVA: 0x0000B1BC File Offset: 0x000093BC
    private void OnEnable()
    {
        foreach (Renderer renderer in this.renderers)
        {
            List<Material> list = renderer.sharedMaterials.ToList<Material>();
            list.Add(this.outlineMaskMaterial);
            list.Add(this.outlineFillMaterial);
            renderer.materials = list.ToArray();
        }
    }

    // Token: 0x06000252 RID: 594 RVA: 0x0000B210 File Offset: 0x00009410
    private void OnMouseOver()
    {
        this.outlineWidth = 10f;
        this.needsUpdate = true;
    }

    // Token: 0x06000253 RID: 595 RVA: 0x0000B224 File Offset: 0x00009424
    private void OnMouseExit()
    {
        this.outlineWidth = 0f;
        this.needsUpdate = true;
    }

    // Token: 0x06000254 RID: 596 RVA: 0x0000B238 File Offset: 0x00009438
    private void OnValidate()
    {
        this.needsUpdate = true;
        if ((!this.precomputeOutline && this.bakeKeys.Count != 0) || this.bakeKeys.Count != this.bakeValues.Count)
        {
            this.bakeKeys.Clear();
            this.bakeValues.Clear();
        }
        if (this.precomputeOutline && this.bakeKeys.Count == 0)
        {
            this.Bake();
        }
    }

    // Token: 0x06000255 RID: 597 RVA: 0x0000B2AA File Offset: 0x000094AA
    private void Update()
    {
        if (this.needsUpdate)
        {
            this.needsUpdate = false;
            this.UpdateMaterialProperties();
        }
    }

    // Token: 0x06000256 RID: 598 RVA: 0x0000B2C4 File Offset: 0x000094C4
    private void OnDisable()
    {
        foreach (Renderer renderer in this.renderers)
        {
            List<Material> list = renderer.sharedMaterials.ToList<Material>();
            list.Remove(this.outlineMaskMaterial);
            list.Remove(this.outlineFillMaterial);
            renderer.materials = list.ToArray();
        }
    }

    // Token: 0x06000257 RID: 599 RVA: 0x0000B31A File Offset: 0x0000951A
    private void OnDestroy()
    {
        Object.Destroy(this.outlineMaskMaterial);
        Object.Destroy(this.outlineFillMaterial);
    }

    // Token: 0x06000258 RID: 600 RVA: 0x0000B334 File Offset: 0x00009534
    private void Bake()
    {
        HashSet<Mesh> hashSet = new HashSet<Mesh>();
        foreach (MeshFilter meshFilter in base.GetComponentsInChildren<MeshFilter>())
        {
            if (hashSet.Add(meshFilter.sharedMesh))
            {
                List<Vector3> data = this.SmoothNormals(meshFilter.sharedMesh);
                this.bakeKeys.Add(meshFilter.sharedMesh);
                this.bakeValues.Add(new Outline.ListVector3
                {
                    data = data
                });
            }
        }
    }

    // Token: 0x06000259 RID: 601 RVA: 0x0000B3A8 File Offset: 0x000095A8
    private void LoadSmoothNormals()
    {
        foreach (MeshFilter meshFilter in base.GetComponentsInChildren<MeshFilter>())
        {
            if (Outline.registeredMeshes.Add(meshFilter.sharedMesh))
            {
                int num = this.bakeKeys.IndexOf(meshFilter.sharedMesh);
                List<Vector3> uvs = (num >= 0) ? this.bakeValues[num].data : this.SmoothNormals(meshFilter.sharedMesh);
                meshFilter.sharedMesh.SetUVs(3, uvs);
                Renderer component = meshFilter.GetComponent<Renderer>();
                if (component != null)
                {
                    this.CombineSubmeshes(meshFilter.sharedMesh, component.sharedMaterials);
                }
            }
        }
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in base.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (Outline.registeredMeshes.Add(skinnedMeshRenderer.sharedMesh))
            {
                skinnedMeshRenderer.sharedMesh.uv4 = new Vector2[skinnedMeshRenderer.sharedMesh.vertexCount];
                this.CombineSubmeshes(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials);
            }
        }
    }

    // Token: 0x0600025A RID: 602 RVA: 0x0000B4B4 File Offset: 0x000096B4
    private List<Vector3> SmoothNormals(Mesh mesh)
    {
        IEnumerable<IGrouping<Vector3, KeyValuePair<Vector3, int>>> enumerable = from pair in mesh.vertices.Select((Vector3 vertex, int index) => new KeyValuePair<Vector3, int>(vertex, index))
                                                                                 group pair by pair.Key;
        List<Vector3> list = new List<Vector3>(mesh.normals);
        foreach (IGrouping<Vector3, KeyValuePair<Vector3, int>> grouping in enumerable)
        {
            if (grouping.Count<KeyValuePair<Vector3, int>>() != 1)
            {
                Vector3 vector = Vector3.zero;
                foreach (KeyValuePair<Vector3, int> keyValuePair in grouping)
                {
                    vector += list[keyValuePair.Value];
                }
                vector.Normalize();
                foreach (KeyValuePair<Vector3, int> keyValuePair2 in grouping)
                {
                    list[keyValuePair2.Value] = vector;
                }
            }
        }
        return list;
    }

    // Token: 0x0600025B RID: 603 RVA: 0x0000B5FC File Offset: 0x000097FC
    private void CombineSubmeshes(Mesh mesh, Material[] materials)
    {
        if (mesh.subMeshCount == 1)
        {
            return;
        }
        if (mesh.subMeshCount > materials.Length)
        {
            return;
        }
        int subMeshCount = mesh.subMeshCount;
        mesh.subMeshCount = subMeshCount + 1;
        mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1);
    }

    // Token: 0x0600025C RID: 604 RVA: 0x0000B644 File Offset: 0x00009844
    private void UpdateMaterialProperties()
    {
        this.outlineFillMaterial.SetColor("_OutlineColor", this.outlineColor);
        switch (this.outlineMode)
        {
            case Outline.Mode.OutlineAll:
                this.outlineMaskMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.OutlineVisible:
                this.outlineMaskMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_ZTest", 4f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.OutlineHidden:
                this.outlineMaskMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_ZTest", 5f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.OutlineAndSilhouette:
                this.outlineMaskMaterial.SetFloat("_ZTest", 4f);
                this.outlineFillMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.SilhouetteOnly:
                this.outlineMaskMaterial.SetFloat("_ZTest", 4f);
                this.outlineFillMaterial.SetFloat("_ZTest", 5f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", 0f);
                return;
            default:
                return;
        }
    }

    // Token: 0x040002DA RID: 730
    private static HashSet<Mesh> registeredMeshes = new HashSet<Mesh>();

    // Token: 0x040002DB RID: 731
    [SerializeField]
    private Outline.Mode outlineMode;

    // Token: 0x040002DC RID: 732
    [SerializeField]
    private Color outlineColor = Color.white;

    // Token: 0x040002DD RID: 733
    [SerializeField]
    [Range(0f, 10f)]
    private float outlineWidth = 2f;

    // Token: 0x040002DE RID: 734
    [Header("Optional")]
    [SerializeField]
    [Tooltip("Precompute enabled: Per-vertex calculations are performed in the editor and serialized with the object. Precompute disabled: Per-vertex calculations are performed at runtime in Awake(). This may cause a pause for large meshes.")]
    private bool precomputeOutline;

    // Token: 0x040002DF RID: 735
    [SerializeField]
    [HideInInspector]
    private List<Mesh> bakeKeys = new List<Mesh>();

    // Token: 0x040002E0 RID: 736
    [SerializeField]
    [HideInInspector]
    private List<Outline.ListVector3> bakeValues = new List<Outline.ListVector3>();

    // Token: 0x040002E1 RID: 737
    private Renderer[] renderers;

    // Token: 0x040002E2 RID: 738
    private Material outlineMaskMaterial;

    // Token: 0x040002E3 RID: 739
    private Material outlineFillMaterial;

    // Token: 0x040002E4 RID: 740
    private bool needsUpdate;

    // Token: 0x020000D6 RID: 214
    public enum Mode
    {
        // Token: 0x04000496 RID: 1174
        OutlineAll,
        // Token: 0x04000497 RID: 1175
        OutlineVisible,
        // Token: 0x04000498 RID: 1176
        OutlineHidden,
        // Token: 0x04000499 RID: 1177
        OutlineAndSilhouette,
        // Token: 0x0400049A RID: 1178
        SilhouetteOnly
    }

    // Token: 0x020000D7 RID: 215
    [Serializable]
    private class ListVector3
    {
        // Token: 0x0400049B RID: 1179
        public List<Vector3> data;
    }
}

using System;
using UnityEngine;

// Token: 0x02000061 RID: 97
public class PauseMenu : MonoBehaviour
{
    // Token: 0x060001EF RID: 495 RVA: 0x0000930E File Offset: 0x0000750E
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!this.pausePanel.activeInHierarchy)
            {
                this.OnPause();
                return;
            }
            this.OnResume();
        }
    }

    // Token: 0x060001F0 RID: 496 RVA: 0x00009333 File Offset: 0x00007533
    public void OnPause()
    {
        this.pausePanel.SetActive(true);
        this.lockPanel.SetActive(false);
        this.UnlockCursor();
    }

    // Token: 0x060001F1 RID: 497 RVA: 0x00009353 File Offset: 0x00007553
    public void OnResume()
    {
        this.pausePanel.SetActive(false);
        if (this.cardGame != null && this.cardGame.activeInHierarchy)
        {
            Time.timeScale = 1f;
            return;
        }
        this.LockCursor();
    }

    // Token: 0x060001F2 RID: 498 RVA: 0x0000938D File Offset: 0x0000758D
    public void LockCursor()
    {
        CursorController.LockCursor();
        Time.timeScale = 1f;
    }

    // Token: 0x060001F3 RID: 499 RVA: 0x0000939E File Offset: 0x0000759E
    public void UnlockCursor()
    {
        CursorController.UnlockCursor();
        Time.timeScale = 0f;
    }

    // Token: 0x0400025E RID: 606
    public GameObject pausePanel;

    // Token: 0x0400025F RID: 607
    public GameObject lockPanel;

    // Token: 0x04000260 RID: 608
    public GameObject cardGame;
}

using System;
using UnityEngine;

// Token: 0x02000040 RID: 64
public class Pier : MonoBehaviour
{
    // Token: 0x06000157 RID: 343 RVA: 0x00006C72 File Offset: 0x00004E72
    private void Start()
    {
        this.LoadPresence();
        this.CheckPresence();
    }

    // Token: 0x06000158 RID: 344 RVA: 0x00006C80 File Offset: 0x00004E80
    private void LoadPresence()
    {
        this.presence = PlayerPrefs.GetString("Pier", "true");
    }

    // Token: 0x06000159 RID: 345 RVA: 0x00006C97 File Offset: 0x00004E97
    public void SavePresence()
    {
        PlayerPrefs.SetString("Pier", this.presence);
        this.CheckPresence();
    }

    // Token: 0x0600015A RID: 346 RVA: 0x00006CB0 File Offset: 0x00004EB0
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.boat.pierPos = this.pierPos;
            this.boat.pierIsNear = true;
            this.boat.boatText.gameObject.SetActive(true);
        }
    }

    // Token: 0x0600015B RID: 347 RVA: 0x00006CFD File Offset: 0x00004EFD
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.boat.pierIsNear = false;
            this.boat.boatText.gameObject.SetActive(false);
        }
    }

    // Token: 0x0600015C RID: 348 RVA: 0x00006D30 File Offset: 0x00004F30
    public void CheckPresence()
    {
        if (this.presence == "false")
        {
            this.boat.pierIsNear = false;
            this.boat.boatText.gameObject.SetActive(false);
            base.gameObject.SetActive(false);
            return;
        }
        if (this.presence == "true")
        {
            base.gameObject.SetActive(true);
        }
    }

    // Token: 0x04000199 RID: 409
    public BoatSwitch boat;

    // Token: 0x0400019A RID: 410
    public Transform pierPos;

    // Token: 0x0400019B RID: 411
    public bool lastPier;

    // Token: 0x0400019C RID: 412
    public string presence = "true";
}

using System;
using Cinemachine;
using UnityEngine;

// Token: 0x02000053 RID: 83
public class PlayerCamera : MonoBehaviour
{
    // Token: 0x060001AD RID: 429 RVA: 0x0000844D File Offset: 0x0000664D
    private void Start()
    {
        CursorController.LockCursor();
    }

    // Token: 0x060001AE RID: 430 RVA: 0x00008454 File Offset: 0x00006654
    private void Update()
    {
        if (!this.player.isStopped)
        {
            float num = Input.GetAxisRaw("Mouse X") * Time.deltaTime * this.sensitivityX;
            float num2 = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * this.sensitivityY;
            this.yRotation += num;
            this.xRotation -= num2;
            this.xRotation = Mathf.Clamp(this.xRotation, -90f, 90f);
            this.virtualCamera.transform.rotation = Quaternion.Euler(this.xRotation, this.yRotation, 0f);
            this.orientation.rotation = Quaternion.Euler(0f, this.yRotation, 0f);
        }
    }

    // Token: 0x060001AF RID: 431 RVA: 0x0000851E File Offset: 0x0000671E
    public void FlipPlayer()
    {
        this.yRotation += 180f;
    }

    // Token: 0x04000209 RID: 521
    public float sensitivityX;

    // Token: 0x0400020A RID: 522
    public float sensitivityY;

    // Token: 0x0400020B RID: 523
    public PlayerController player;

    // Token: 0x0400020C RID: 524
    public Transform orientation;

    // Token: 0x0400020D RID: 525
    public CinemachineVirtualCamera virtualCamera;

    // Token: 0x0400020E RID: 526
    private float xRotation;

    // Token: 0x0400020F RID: 527
    private float yRotation;
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Token: 0x02000054 RID: 84
public class PlayerController : MonoBehaviour
{
    // Token: 0x060001B1 RID: 433 RVA: 0x0000853C File Offset: 0x0000673C
    private void Start()
    {
        this.rb = base.GetComponent<Rigidbody>();
        this.rb.freezeRotation = true;
        this.readyToJump = true;
        if (GravityController.instance != null)
        {
            GravityController.instance.player = this;
            GravityController.instance.CheckGravity();
        }
        this.LoadPosition();
    }

    // Token: 0x060001B2 RID: 434 RVA: 0x00008590 File Offset: 0x00006790
    private void Update()
    {
        if (!this.isStopped)
        {
            this.grounded = Physics.Raycast(base.transform.position, Vector3.down, this.groundedValue * 0.5f + 0.01f, this.whatIsGround);
            this.MyInput();
            this.SpeedControl();
            if (this.grounded)
            {
                this.rb.drag = this.groundDrag;
                return;
            }
            this.rb.drag = 0f;
        }
    }

    // Token: 0x060001B3 RID: 435 RVA: 0x00008613 File Offset: 0x00006813
    private void FixedUpdate()
    {
        if (!this.isStopped)
        {
            this.MovePlayer();
        }
    }

    // Token: 0x060001B4 RID: 436 RVA: 0x00008624 File Offset: 0x00006824
    private void MyInput()
    {
        this.horizontalInput = Input.GetAxisRaw("Horizontal");
        this.verticalInput = Input.GetAxisRaw("Vertical");
        if (Input.GetKey(this.jumpKey) && this.readyToJump && this.grounded)
        {
            this.readyToJump = false;
            this.Jump();
            base.Invoke("ResetJump", this.jumpCooldown);
        }
    }

    // Token: 0x060001B5 RID: 437 RVA: 0x0000868C File Offset: 0x0000688C
    private void MovePlayer()
    {
        this.moveDirection = this.orientation.forward * this.verticalInput + this.orientation.right * this.horizontalInput;
        if (this.grounded)
        {
            this.rb.AddForce(this.moveDirection.normalized * this.moveSpeed * 10f, ForceMode.Force);
            return;
        }
        if (!this.grounded)
        {
            this.rb.AddForce(this.moveDirection.normalized * this.moveSpeed * 10f * this.airMultiplier, ForceMode.Force);
        }
    }

    // Token: 0x060001B6 RID: 438 RVA: 0x00008744 File Offset: 0x00006944
    private void SpeedControl()
    {
        Vector3 vector = new Vector3(this.rb.velocity.x, 0f, this.rb.velocity.z);
        if (vector.magnitude > this.moveSpeed)
        {
            Vector3 vector2 = vector.normalized * this.moveSpeed;
            this.rb.velocity = new Vector3(vector2.x, this.rb.velocity.y, vector2.z);
        }
    }

    // Token: 0x060001B7 RID: 439 RVA: 0x000087CC File Offset: 0x000069CC
    private void Jump()
    {
        this.rb.velocity = new Vector3(this.rb.velocity.x, 0f, this.rb.velocity.z);
        this.rb.AddForce(base.transform.up * this.jumpForce, ForceMode.Impulse);
    }

    // Token: 0x060001B8 RID: 440 RVA: 0x00008830 File Offset: 0x00006A30
    private void ResetJump()
    {
        this.readyToJump = true;
    }

    // Token: 0x060001B9 RID: 441 RVA: 0x0000883C File Offset: 0x00006A3C
    public void SavePosition()
    {
        this.x = base.transform.position.x;
        this.y = base.transform.position.y;
        this.z = base.transform.position.z;
        if (SceneManager.GetActiveScene().name == this.startSceneName)
        {
            PlayerPrefs.SetFloat("x", this.x);
            PlayerPrefs.SetFloat("y", this.y);
            PlayerPrefs.SetFloat("z", this.z);
        }
        else
        {
            PlayerPrefs.SetFloat("xMoon", this.x);
            PlayerPrefs.SetFloat("yMoon", this.y);
            PlayerPrefs.SetFloat("zMoon", this.z);
        }
        PlayerPrefs.SetString("Gun", this.hasGun);
    }

    // Token: 0x060001BA RID: 442 RVA: 0x00008918 File Offset: 0x00006B18
    public void LoadPosition()
    {
        if (SceneManager.GetActiveScene().name == this.startSceneName)
        {
            this.x = PlayerPrefs.GetFloat("x", 57.47f);
            this.y = PlayerPrefs.GetFloat("y", 20.69f);
            this.z = PlayerPrefs.GetFloat("z", 40.24f);
        }
        else
        {
            this.x = PlayerPrefs.GetFloat("xMoon", 2.931f);
            this.y = PlayerPrefs.GetFloat("yMoon", 5.168f);
            this.z = PlayerPrefs.GetFloat("zMoon", -8.655f);
        }
        this.hasGun = PlayerPrefs.GetString("Gun", "false");
        Vector3 position = new Vector3(this.x, this.y, this.z);
        if (this.hasGun == "true")
        {
            this.gun.SetActive(true);
            this.gunText.SetActive(true);
            this.aim.SetActive(true);
        }
        else
        {
            this.gun.SetActive(false);
            this.gunText.SetActive(false);
            this.aim.SetActive(false);
        }
        base.transform.position = position;
    }

    // Token: 0x060001BB RID: 443 RVA: 0x00008A55 File Offset: 0x00006C55
    public void SetupDialogue(bool mode)
    {
        this.isStopped = mode;
        if (mode)
        {
            CursorController.UnlockCursor();
            return;
        }
        CursorController.LockCursor();
    }

    // Token: 0x04000210 RID: 528
    [Header("Movement")]
    public float moveSpeed;

    // Token: 0x04000211 RID: 529
    public Transform orientation;

    // Token: 0x04000212 RID: 530
    public float groundDrag;

    // Token: 0x04000213 RID: 531
    public float groundedValue;

    // Token: 0x04000214 RID: 532
    public float jumpForce;

    // Token: 0x04000215 RID: 533
    public float jumpCooldown;

    // Token: 0x04000216 RID: 534
    public float airMultiplier;

    // Token: 0x04000217 RID: 535
    public float x;

    // Token: 0x04000218 RID: 536
    public float y;

    // Token: 0x04000219 RID: 537
    public float z;

    // Token: 0x0400021A RID: 538
    public string hasGun = "false";

    // Token: 0x0400021B RID: 539
    public GameObject gun;

    // Token: 0x0400021C RID: 540
    public GameObject aim;

    // Token: 0x0400021D RID: 541
    public GameObject gunText;

    // Token: 0x0400021E RID: 542
    private bool readyToJump;

    // Token: 0x0400021F RID: 543
    [Header("Ground Check")]
    public float playerHeight;

    // Token: 0x04000220 RID: 544
    public LayerMask whatIsGround;

    // Token: 0x04000221 RID: 545
    public bool grounded;

    // Token: 0x04000222 RID: 546
    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;

    // Token: 0x04000223 RID: 547
    private float horizontalInput;

    // Token: 0x04000224 RID: 548
    private float verticalInput;

    // Token: 0x04000225 RID: 549
    [HideInInspector]
    public bool isStopped;

    // Token: 0x04000226 RID: 550
    [Header("Scenes")]
    public string startSceneName;

    // Token: 0x04000227 RID: 551
    private Vector3 moveDirection;

    // Token: 0x04000228 RID: 552
    private Rigidbody rb;
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Token: 0x02000055 RID: 85
public class PlayerHealth : MonoBehaviour
{
    // Token: 0x060001BD RID: 445 RVA: 0x00008A87 File Offset: 0x00006C87
    private void Start()
    {
        this.currentHealth = this.maxHealth;
        this.currectHealCooldown = this.healCooldownAfterDamage;
    }

    // Token: 0x060001BE RID: 446 RVA: 0x00008AA4 File Offset: 0x00006CA4
    private void Update()
    {
        this.bloodOverlay.color = new Color(this.bloodOverlay.color.r, this.bloodOverlay.color.g, this.bloodOverlay.color.b, 1f - this.currentHealth / this.maxHealth);
        if (this.currectHealCooldown > 0f)
        {
            this.currectHealCooldown -= Time.deltaTime;
        }
        else
        {
            this.canHeal = true;
        }
        if (Time.realtimeSinceStartup < this.nextHealthUpdateTime)
        {
            return;
        }
        this.nextHealthUpdateTime = Time.realtimeSinceStartup + this.healSpeed;
        if (this.currentHealth < this.maxHealth && this.canHeal)
        {
            this.currentHealth += this.healAmount;
        }
    }

    // Token: 0x060001BF RID: 447 RVA: 0x00008B78 File Offset: 0x00006D78
    public void TakeDamage(float damage)
    {
        this.canHeal = false;
        this.currectHealCooldown = this.healCooldownAfterDamage;
        this.currentHealth -= damage;
        if (this.currentHealth <= 0f)
        {
            Object.FindObjectOfType<MusicManager>().EnableMusic(0);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // Token: 0x04000229 RID: 553
    public Image bloodOverlay;

    // Token: 0x0400022A RID: 554
    public float maxHealth = 100f;

    // Token: 0x0400022B RID: 555
    public float currentHealth = 100f;

    // Token: 0x0400022C RID: 556
    private float nextHealthUpdateTime;

    // Token: 0x0400022D RID: 557
    public float healSpeed = 1f;

    // Token: 0x0400022E RID: 558
    public float healAmount = 1f;

    // Token: 0x0400022F RID: 559
    public float healCooldownAfterDamage = 5f;

    // Token: 0x04000230 RID: 560
    public float currectHealCooldown;

    // Token: 0x04000231 RID: 561
    private bool canHeal = true;
}

using System;
using UnityEngine;

// Token: 0x0200007C RID: 124
public class PlayerMovement : MonoBehaviour
{
    // Token: 0x06000248 RID: 584 RVA: 0x0000AF78 File Offset: 0x00009178
    private void Update()
    {
        this.isGrounded = Physics.CheckSphere(this.groundCheck.position, this.groundDistance, this.groundMask);
        if (this.isGrounded && this.velocity.y < 0f)
        {
            this.velocity.y = -2f;
        }
        if (Input.GetKey("left shift") && this.isGrounded)
        {
            this.speed = 10f;
        }
        else
        {
            this.speed = 5f;
        }
        float axis = Input.GetAxis("Horizontal");
        float axis2 = Input.GetAxis("Vertical");
        Vector3 a = base.transform.right * axis + base.transform.forward * axis2;
        this.controller.Move(a * this.speed * Time.deltaTime);
        if (Input.GetButtonDown("Jump") && this.isGrounded)
        {
            this.velocity.y = Mathf.Sqrt(this.jumpHeight * -2f * this.gravity);
        }
        this.velocity.y = this.velocity.y + this.gravity * Time.deltaTime;
        this.controller.Move(this.velocity * Time.deltaTime);
    }

    // Token: 0x040002D1 RID: 721
    public CharacterController controller;

    // Token: 0x040002D2 RID: 722
    public float speed = 5f;

    // Token: 0x040002D3 RID: 723
    public float gravity = -9.18f;

    // Token: 0x040002D4 RID: 724
    public float jumpHeight = 3f;

    // Token: 0x040002D5 RID: 725
    public Transform groundCheck;

    // Token: 0x040002D6 RID: 726
    public float groundDistance = 0.4f;

    // Token: 0x040002D7 RID: 727
    public LayerMask groundMask;

    // Token: 0x040002D8 RID: 728
    private Vector3 velocity;

    // Token: 0x040002D9 RID: 729
    private bool isGrounded;
}

using System;
using UnityEngine;

// Token: 0x0200006A RID: 106
public class ProjectileAddon : MonoBehaviour
{
    // Token: 0x0600021A RID: 538 RVA: 0x0000A3DE File Offset: 0x000085DE
    private void Start()
    {
        this.rb = base.GetComponent<Rigidbody>();
    }

    // Token: 0x0600021B RID: 539 RVA: 0x0000A3EC File Offset: 0x000085EC
    private void OnCollisionEnter(Collision collision)
    {
        if (this.targetHit)
        {
            return;
        }
        this.targetHit = true;
        if (collision.gameObject.GetComponent<BasicEnemy>() != null)
        {
            collision.gameObject.GetComponent<BasicEnemy>().TakeDamage(this.damage);
            Object.Destroy(base.gameObject);
        }
        this.rb.isKinematic = true;
        base.transform.SetParent(collision.transform);
    }

    // Token: 0x04000289 RID: 649
    public int damage;

    // Token: 0x0400028A RID: 650
    private Rigidbody rb;

    // Token: 0x0400028B RID: 651
    private bool targetHit;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000030 RID: 48
public class ProjectileGun : MonoBehaviour
{
    // Token: 0x060000F9 RID: 249 RVA: 0x00005506 File Offset: 0x00003706
    private void Awake()
    {
        this._animator = base.GetComponent<Animator>();
        this.bulletsLeft = this.magazineSize;
        this.readyToShoot = true;
    }

    // Token: 0x060000FA RID: 250 RVA: 0x00005527 File Offset: 0x00003727
    private void Update()
    {
        this.MyInput();
    }

    // Token: 0x060000FB RID: 251 RVA: 0x00005530 File Offset: 0x00003730
    private void MyInput()
    {
        if (this.allowButtonHold)
        {
            this.shooting = Input.GetKey(KeyCode.Mouse0);
        }
        else
        {
            this.shooting = Input.GetKeyDown(KeyCode.Mouse0);
        }
        if (Input.GetKeyDown(KeyCode.R) && this.bulletsLeft < this.magazineSize && !this.reloading)
        {
            this.Reload();
        }
        if (this.readyToShoot && this.shooting && !this.reloading && this.bulletsLeft <= 0)
        {
            this.Reload();
        }
        if (this.readyToShoot && this.shooting && !this.reloading && this.bulletsLeft > 0)
        {
            this.bulletsShot = 0;
            this.Shoot();
        }
    }

    // Token: 0x060000FC RID: 252 RVA: 0x000055E4 File Offset: 0x000037E4
    private void Shoot()
    {
        this.readyToShoot = false;
        this.cylinderAnimator.SetTrigger("hasShot");
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit raycastHit;
        Vector3 point;
        if (Physics.Raycast(ray, out raycastHit))
        {
            point = raycastHit.point;
        }
        else
        {
            point = ray.GetPoint(75f);
        }
        Vector3 a = point - this.attackPoint.position;
        float x = Random.Range(-this.spread, this.spread);
        float y = Random.Range(-this.spread, this.spread);
        Vector3 vector = a + new Vector3(x, y, 0f);
        GameObject gameObject = Object.Instantiate<GameObject>(this.bullet, this.attackPoint.position, Quaternion.identity);
        gameObject.transform.forward = vector.normalized;
        gameObject.GetComponent<Rigidbody>().AddForce(vector.normalized * this.shootForce, ForceMode.Impulse);
        gameObject.GetComponent<Rigidbody>().AddForce(Camera.main.transform.up * this.upwardForce, ForceMode.Impulse);
        if (this.muzzleFlash != null)
        {
            Object.Instantiate<GameObject>(this.muzzleFlash, this.attackPoint.position, Quaternion.identity).transform.parent = this.attackPoint;
        }
        this.bulletsLeft--;
        this.bulletsShot++;
        this.UpdateText();
        if (this.allowInvoke)
        {
            base.Invoke("ResetShot", this.timeBetweenShooting);
            this.allowInvoke = false;
            this.playerRb.AddForce(-vector.normalized * this.recoilForce, ForceMode.Impulse);
        }
        if (this.bulletsShot < this.bulletsPerTap && this.bulletsLeft > 0)
        {
            base.Invoke("Shoot", this.timeBetweenShots);
        }
    }

    // Token: 0x060000FD RID: 253 RVA: 0x000057CA File Offset: 0x000039CA
    private void ResetShot()
    {
        this.readyToShoot = true;
        this.allowInvoke = true;
    }

    // Token: 0x060000FE RID: 254 RVA: 0x000057DC File Offset: 0x000039DC
    private void Reload()
    {
        this._animator.SetTrigger("startReloading");
        this.reloading = true;
        this.ammunitionDisplay.enabled = false;
        this.ammunitionDisplay.transform.GetChild(0).gameObject.SetActive(true);
        base.Invoke("ReloadFinished", this.reloadTime);
    }

    // Token: 0x060000FF RID: 255 RVA: 0x0000583C File Offset: 0x00003A3C
    private void ReloadFinished()
    {
        this.ammunitionDisplay.enabled = true;
        this.ammunitionDisplay.transform.GetChild(0).gameObject.SetActive(false);
        this.bulletsLeft = this.magazineSize;
        this.reloading = false;
        this.UpdateText();
    }

    // Token: 0x06000100 RID: 256 RVA: 0x0000588C File Offset: 0x00003A8C
    private void UpdateText()
    {
        if (this.ammunitionDisplay != null)
        {
            this.ammunitionDisplay.SetText((this.bulletsLeft / this.bulletsPerTap).ToString() + " / " + (this.magazineSize / this.bulletsPerTap).ToString(), true);
        }
    }

    // Token: 0x06000101 RID: 257 RVA: 0x000058E7 File Offset: 0x00003AE7
    public void TriggerCylinderReload()
    {
        this.cylinderAnimator.SetTrigger("fullReload");
    }

    // Token: 0x0400010F RID: 271
    public GameObject bullet;

    // Token: 0x04000110 RID: 272
    public float shootForce;

    // Token: 0x04000111 RID: 273
    public float upwardForce;

    // Token: 0x04000112 RID: 274
    public float timeBetweenShooting;

    // Token: 0x04000113 RID: 275
    public float spread;

    // Token: 0x04000114 RID: 276
    public float reloadTime;

    // Token: 0x04000115 RID: 277
    public float timeBetweenShots;

    // Token: 0x04000116 RID: 278
    public int magazineSize;

    // Token: 0x04000117 RID: 279
    public int bulletsPerTap;

    // Token: 0x04000118 RID: 280
    public bool allowButtonHold;

    // Token: 0x04000119 RID: 281
    private int bulletsLeft;

    // Token: 0x0400011A RID: 282
    private int bulletsShot;

    // Token: 0x0400011B RID: 283
    public Rigidbody playerRb;

    // Token: 0x0400011C RID: 284
    public float recoilForce;

    // Token: 0x0400011D RID: 285
    private bool shooting;

    // Token: 0x0400011E RID: 286
    private bool readyToShoot;

    // Token: 0x0400011F RID: 287
    private bool reloading;

    // Token: 0x04000120 RID: 288
    public Transform attackPoint;

    // Token: 0x04000121 RID: 289
    public GameObject muzzleFlash;

    // Token: 0x04000122 RID: 290
    public GameObject gunText;

    // Token: 0x04000123 RID: 291
    public TextMeshProUGUI ammunitionDisplay;

    // Token: 0x04000124 RID: 292
    public Animator cylinderAnimator;

    // Token: 0x04000125 RID: 293
    public bool allowInvoke = true;

    // Token: 0x04000126 RID: 294
    private Animator _animator;
}

using System;
using UnityEngine;

// Token: 0x0200002D RID: 45
public class RunOver : MonoBehaviour
{
    // Token: 0x060000ED RID: 237 RVA: 0x00005372 File Offset: 0x00003572
    private void Start()
    {
        this.gateAnimator.SetBool("hasMushrooms", true);
    }

    // Token: 0x060000EE RID: 238 RVA: 0x00005385 File Offset: 0x00003585
    private void Update()
    {
        this.hasFinishedRun = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        if (this.hasFinishedRun)
        {
            this.EndRun();
        }
    }

    // Token: 0x060000EF RID: 239 RVA: 0x000053BC File Offset: 0x000035BC
    public void EndRun()
    {
        this.enemyRun.TakeDamage(50);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.FindObjectOfType<PlayerController>().moveSpeed = 3.5f;
        this.gateAnimator.SetBool("pastGate", true);
        base.enabled = false;
    }

    // Token: 0x060000F0 RID: 240 RVA: 0x00005408 File Offset: 0x00003608
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x04000102 RID: 258
    public Animator gateAnimator;

    // Token: 0x04000103 RID: 259
    public Enemy enemyRun;

    // Token: 0x04000104 RID: 260
    public LayerMask whatIsPlayer;

    // Token: 0x04000105 RID: 261
    public float sightRange;

    // Token: 0x04000106 RID: 262
    private bool hasFinishedRun;
}

using System;
using System.Collections;
using UnityEngine;

// Token: 0x0200006B RID: 107
public class Sable : MonoBehaviour
{
    // Token: 0x0600021D RID: 541 RVA: 0x0000A464 File Offset: 0x00008664
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            int num = Random.Range(1, 5);
            int num2 = Random.Range(0, this.swingSounds.Length);
            this.weaponAnimator.Play(num.ToString());
            Object.Instantiate<GameObject>(this.swingSounds[num2], base.transform.position, Quaternion.identity);
            this.clickChecker = this.ClickCheck();
            base.StartCoroutine(this.clickChecker);
        }
    }

    // Token: 0x0600021E RID: 542 RVA: 0x0000A4DC File Offset: 0x000086DC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Enemy enemy;
            if (other.transform.root.TryGetComponent<Enemy>(out enemy) && this.clickChecker != null)
            {
                Debug.Log("NYAH");
                enemy.TakeDamage(this.damage);
                this.InstantiateSounds(other);
                base.StopCoroutine(this.clickChecker);
                this.clickChecker = null;
                return;
            }
            Shailushay shailushay;
            if (other.transform.root.TryGetComponent<Shailushay>(out shailushay) && this.clickChecker != null)
            {
                shailushay.TakeDamage(this.damage);
                this.InstantiateSounds(other);
                base.StopCoroutine(this.clickChecker);
                this.clickChecker = null;
            }
        }
    }

    // Token: 0x0600021F RID: 543 RVA: 0x0000A588 File Offset: 0x00008788
    private void InstantiateSounds(Collider other)
    {
        int num = Random.Range(0, this.damageSounds.Length);
        Object.Instantiate<GameObject>(this.damageSounds[num], base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.damageEffect, other.ClosestPointOnBounds(base.transform.position), Quaternion.identity);
    }

    // Token: 0x06000220 RID: 544 RVA: 0x0000A5E4 File Offset: 0x000087E4
    private IEnumerator ClickCheck()
    {
        yield return new WaitForSeconds(0.2f);
        this.clickChecker = null;
        yield break;
    }

    // Token: 0x0400028C RID: 652
    public int damage;

    // Token: 0x0400028D RID: 653
    public GameObject[] damageSounds;

    // Token: 0x0400028E RID: 654
    public Animator weaponAnimator;

    // Token: 0x0400028F RID: 655
    public GameObject[] swingSounds;

    // Token: 0x04000290 RID: 656
    public GameObject damageEffect;

    // Token: 0x04000291 RID: 657
    private Vector3 sparksDirection;

    // Token: 0x04000292 RID: 658
    private IEnumerator clickChecker;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000062 RID: 98
public class SaveDropdownValue : MonoBehaviour
{
    // Token: 0x060001F5 RID: 501 RVA: 0x000093B7 File Offset: 0x000075B7
    private void Awake()
    {
        this.dropdown = base.GetComponent<TMP_Dropdown>();
        this.dropdown.onValueChanged.AddListener(delegate (int index)
        {
            PlayerPrefs.SetInt("optionvalue", this.dropdown.value);
            PlayerPrefs.Save();
        });
    }

    // Token: 0x060001F6 RID: 502 RVA: 0x000093E1 File Offset: 0x000075E1
    private void Start()
    {
        this.dropdown.value = PlayerPrefs.GetInt("optionvalue", 2);
    }

    // Token: 0x04000261 RID: 609
    private const string PrefName = "optionvalue";

    // Token: 0x04000262 RID: 610
    private TMP_Dropdown dropdown;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Token: 0x02000057 RID: 87
public class SceneLoader : MonoBehaviour
{
    // Token: 0x060001C6 RID: 454 RVA: 0x00008D3D File Offset: 0x00006F3D
    private void OnEnable()
    {
        this.LoadScene(this.sceneIndex);
    }

    // Token: 0x060001C7 RID: 455 RVA: 0x00008D4B File Offset: 0x00006F4B
    public void LoadScene(int index)
    {
        if (index == 2)
        {
            PlayerPrefs.SetInt("Scene", 2);
        }
        base.StartCoroutine(this.LoadAsync(index));
    }

    // Token: 0x060001C8 RID: 456 RVA: 0x00008D6A File Offset: 0x00006F6A
    public IEnumerator LoadAsync(int index)
    {
        this.progressSlider.value = 0f;
        this.LoaderUI.SetActive(true);
        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(index);
        asyncOperation.allowSceneActivation = false;
        float progress = 0f;
        while (!asyncOperation.isDone)
        {
            progress = Mathf.MoveTowards(progress, asyncOperation.progress, Time.deltaTime);
            this.progressSlider.value = progress;
            if (progress >= 0.9f)
            {
                this.progressSlider.value = 1f;
                asyncOperation.allowSceneActivation = true;
            }
            yield return null;
        }
        yield break;
    }

    // Token: 0x0400023A RID: 570
    public int sceneIndex;

    // Token: 0x0400023B RID: 571
    public GameObject LoaderUI;

    // Token: 0x0400023C RID: 572
    public Slider progressSlider;
}

using System;
using UnityEngine;

// Token: 0x0200001B RID: 27
public class SceneSaver : MonoBehaviour
{
    // Token: 0x06000074 RID: 116 RVA: 0x00003546 File Offset: 0x00001746
    private void Start()
    {
    }

    // Token: 0x06000075 RID: 117 RVA: 0x00003548 File Offset: 0x00001748
    private void Update()
    {
    }
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000063 RID: 99
public class SetQuality : MonoBehaviour
{
    // Token: 0x060001F9 RID: 505 RVA: 0x0000941D File Offset: 0x0000761D
    public void SetQualityLevel(int index)
    {
        QualitySettings.SetQualityLevel(index, false);
        this.settings.SaveQuality(index);
    }

    // Token: 0x04000263 RID: 611
    [SerializeField]
    private TMP_Dropdown qualityDropdown;

    // Token: 0x04000264 RID: 612
    [SerializeField]
    private GameSettings settings;
}

using System;
using UnityEngine;

// Token: 0x02000041 RID: 65
public class ShaiActivator : MonoBehaviour
{
    // Token: 0x0600015E RID: 350 RVA: 0x00006DAF File Offset: 0x00004FAF
    private void Start()
    {
        this.music = Object.FindObjectOfType<MusicManager>();
    }

    // Token: 0x0600015F RID: 351 RVA: 0x00006DBC File Offset: 0x00004FBC
    public void OnPick()
    {
        this.shaiBoss.SetActive(true);
        this.gate.SetActive(true);
        if (!(this.music.audioSource.clip != this.music.musicVariants[5]) && !(this.music.audioSource.clip != this.music.musicVariants[6]))
        {
            this.music.EnableMusic(0);
            return;
        }
        if (Random.Range(0, 10) == 9)
        {
            Debug.Log("POVEZLOOO");
            this.music.EnableMusic(6);
            return;
        }
        this.music.EnableMusic(5);
    }

    // Token: 0x0400019D RID: 413
    public GameObject shaiBoss;

    // Token: 0x0400019E RID: 414
    public GameObject gate;

    // Token: 0x0400019F RID: 415
    private MusicManager music;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Token: 0x02000026 RID: 38
public class Shailushay : MonoBehaviour
{
    // Token: 0x060000CA RID: 202 RVA: 0x00004BBE File Offset: 0x00002DBE
    private void Awake()
    {
        this.agent = base.GetComponent<NavMeshAgent>();
        this.animator = base.GetComponentInChildren<Animator>();
        this.health = this.startHealth;
    }

    // Token: 0x060000CB RID: 203 RVA: 0x00004BE4 File Offset: 0x00002DE4
    private void Start()
    {
        this.healthSlider.gameObject.SetActive(true);
        this.damage = this.startDamage;
    }

    // Token: 0x060000CC RID: 204 RVA: 0x00004C04 File Offset: 0x00002E04
    private void Update()
    {
        this.healthSlider.value = this.health / this.startHealth;
        this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
        if (!this.playerInSightRange && !this.playerInAttackRange)
        {
            this.Patroling();
        }
        if (this.playerInSightRange && !this.playerInAttackRange)
        {
            this.ChasePlayer();
        }
        if (this.playerInAttackRange && this.playerInSightRange)
        {
            this.AttackPlayer();
        }
    }

    // Token: 0x060000CD RID: 205 RVA: 0x00004CBC File Offset: 0x00002EBC
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x060000CE RID: 206 RVA: 0x00004D20 File Offset: 0x00002F20
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x060000CF RID: 207 RVA: 0x00004DC6 File Offset: 0x00002FC6
    private void ChasePlayer()
    {
        this.agent.SetDestination(this.player.position);
        if (!this.isAttacking)
        {
            this.isAttacking = true;
            base.StartCoroutine(this.Rest());
        }
    }

    // Token: 0x060000D0 RID: 208 RVA: 0x00004DFC File Offset: 0x00002FFC
    private void AttackPlayer()
    {
        this.agent.SetDestination(base.transform.position);
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x060000D1 RID: 209 RVA: 0x00004E98 File Offset: 0x00003098
    public void ChooseMoveset()
    {
        int num = Random.Range(0, 5);
        if (num == 0)
        {
            base.StartCoroutine(this.SpawnGroupSticks(5, 0.25f));
            if (this.health < 45f)
            {
                for (int i = 0; i < 7; i++)
                {
                    base.StartCoroutine(this.SummonLightning());
                }
                return;
            }
        }
        else if (num == 1)
        {
            base.StartCoroutine(this.SpawnGroupSticks(8, 0.15f));
            if (this.health < 45f)
            {
                for (int j = 0; j < 7; j++)
                {
                    base.StartCoroutine(this.SummonLightning());
                }
                return;
            }
        }
        else
        {
            if (num == 2 || num == 3)
            {
                base.StartCoroutine(this.SpeedUp());
                return;
            }
            if (num == 4)
            {
                base.StartCoroutine(this.SummonMushrooms());
            }
        }
    }

    // Token: 0x060000D2 RID: 210 RVA: 0x00004F4F File Offset: 0x0000314F
    public IEnumerator SpawnGroupSticks(int totalSticks, float cooldown)
    {
        int numberOfSticks = 0;
        while (numberOfSticks <= totalSticks)
        {
            this.SpawnStick();
            int num = numberOfSticks;
            numberOfSticks = num + 1;
            yield return new WaitForSeconds(cooldown);
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x060000D3 RID: 211 RVA: 0x00004F6C File Offset: 0x0000316C
    public void SpawnStick()
    {
        Object.Instantiate<GameObject>(this.stick, this.attackPos.position, this.attackPos.rotation);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
    }

    // Token: 0x060000D4 RID: 212 RVA: 0x00004FAC File Offset: 0x000031AC
    public IEnumerator SummonLightning()
    {
        Vector3 lightningPos = this.RandomPointInBox(new Vector3(15f, 0f, 15f));
        Object.Instantiate<GameObject>(this.preLightning, lightningPos, Quaternion.identity);
        yield return new WaitForSeconds(1f);
        Object.Instantiate<GameObject>(this.lightning, lightningPos, Quaternion.identity);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
        yield break;
    }

    // Token: 0x060000D5 RID: 213 RVA: 0x00004FBB File Offset: 0x000031BB
    public IEnumerator SummonMushrooms()
    {
        if (this.health > 45f)
        {
            this.easySpawner.enabled = true;
            yield return new WaitForSeconds(2f);
            this.easySpawner.enabled = false;
        }
        else
        {
            this.hardSpawner.enabled = true;
            yield return new WaitForSeconds(3f);
            this.hardSpawner.enabled = false;
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x060000D6 RID: 214 RVA: 0x00004FCA File Offset: 0x000031CA
    public IEnumerator Rest()
    {
        this.restCount++;
        if (this.restCount > 2)
        {
            this.animator.SetBool("isStanding", true);
            this.agent.speed = 0f;
            this.damage = 0;
            yield return new WaitForSeconds(4.5f);
            this.restCount = 0;
            this.animator.SetBool("isStanding", false);
            this.damage = this.startDamage;
            this.agent.speed = this.startSpeed;
        }
        this.ChooseMoveset();
        yield break;
    }

    // Token: 0x060000D7 RID: 215 RVA: 0x00004FD9 File Offset: 0x000031D9
    public IEnumerator SpeedUp()
    {
        this.restCount += 3;
        this.agent.speed = this.startSpeed * 2.4f;
        this.animator.SetFloat("runSpeed", 2f);
        yield return new WaitForSeconds(7f);
        this.agent.speed = this.startSpeed;
        this.animator.SetFloat("runSpeed", 1f);
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x060000D8 RID: 216 RVA: 0x00004FE8 File Offset: 0x000031E8
    private Vector3 RandomPointInBox(Vector3 size)
    {
        return new Vector3(this.player.position.x, 19f, this.player.position.z) + new Vector3((Random.value - 0.5f) * size.x, (Random.value - 0.5f) * size.y, (Random.value - 0.5f) * size.z);
    }

    // Token: 0x060000D9 RID: 217 RVA: 0x0000505F File Offset: 0x0000325F
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x060000DA RID: 218 RVA: 0x00005068 File Offset: 0x00003268
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x060000DB RID: 219 RVA: 0x00005098 File Offset: 0x00003298
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.finalMushroom, base.transform.position, Quaternion.identity);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x060000DC RID: 220 RVA: 0x000050F4 File Offset: 0x000032F4
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x040000D3 RID: 211
    public LayerMask whatIsGround;

    // Token: 0x040000D4 RID: 212
    public LayerMask whatIsPlayer;

    // Token: 0x040000D5 RID: 213
    public Vector3 walkPoint;

    // Token: 0x040000D6 RID: 214
    private bool walkPointSet;

    // Token: 0x040000D7 RID: 215
    public float walkPointRange;

    // Token: 0x040000D8 RID: 216
    public float timeBetweenAttacks;

    // Token: 0x040000D9 RID: 217
    private bool isAttacking;

    // Token: 0x040000DA RID: 218
    private bool alreadyAttacked;

    // Token: 0x040000DB RID: 219
    public float sightRange;

    // Token: 0x040000DC RID: 220
    public float attackRange;

    // Token: 0x040000DD RID: 221
    public bool playerInSightRange;

    // Token: 0x040000DE RID: 222
    public bool playerInAttackRange;

    // Token: 0x040000DF RID: 223
    public float startHealth;

    // Token: 0x040000E0 RID: 224
    public float health;

    // Token: 0x040000E1 RID: 225
    public int startDamage;

    // Token: 0x040000E2 RID: 226
    public float startSpeed;

    // Token: 0x040000E3 RID: 227
    private int damage;

    // Token: 0x040000E4 RID: 228
    private int restCount;

    // Token: 0x040000E5 RID: 229
    public GameObject stick;

    // Token: 0x040000E6 RID: 230
    public GameObject preLightning;

    // Token: 0x040000E7 RID: 231
    public GameObject lightning;

    // Token: 0x040000E8 RID: 232
    public Transform attackPos;

    // Token: 0x040000E9 RID: 233
    public Transform lightningCenter;

    // Token: 0x040000EA RID: 234
    public GameObject magicSound;

    // Token: 0x040000EB RID: 235
    public GameObject deathEffect;

    // Token: 0x040000EC RID: 236
    public GameObject finalMushroom;

    // Token: 0x040000ED RID: 237
    public Slider healthSlider;

    // Token: 0x040000EE RID: 238
    private NavMeshAgent agent;

    // Token: 0x040000EF RID: 239
    public Transform player;

    // Token: 0x040000F0 RID: 240
    private Animator animator;

    // Token: 0x040000F1 RID: 241
    private GameObject newStick;

    // Token: 0x040000F2 RID: 242
    private CurveSpawner easySpawner;

    // Token: 0x040000F3 RID: 243
    private CurveSpawner hardSpawner;

    // Token: 0x040000F4 RID: 244
    private Vector3 stickDirection;
}

using System;
using UnityEngine;

// Token: 0x0200002E RID: 46
public class ShipScreamer : MonoBehaviour
{
    // Token: 0x060000F2 RID: 242 RVA: 0x00005432 File Offset: 0x00003632
    private void OnTriggerEnter(Collider other)
    {
        if (this.sable.activeInHierarchy)
        {
            this.screamer.SetActive(true);
        }
    }

    // Token: 0x04000107 RID: 263
    public GameObject screamer;

    // Token: 0x04000108 RID: 264
    public GameObject sable;
}

using System;
using UnityEngine;

// Token: 0x02000027 RID: 39
public class SmallChanceSpawner : MonoBehaviour
{
    // Token: 0x060000DE RID: 222 RVA: 0x00005149 File Offset: 0x00003349
    private void Start()
    {
        if (Random.Range(0, 5) == 0)
        {
            Object.Instantiate<GameObject>(this.spawnable, base.transform.position, Quaternion.identity);
        }
    }

    // Token: 0x040000F5 RID: 245
    public GameObject spawnable;
}

using System;
using UnityEngine;

// Token: 0x0200005B RID: 91
public class SoundManager : MonoBehaviour
{
    // Token: 0x060001D3 RID: 467 RVA: 0x00008E84 File Offset: 0x00007084
    private void Start()
    {
        if (this.isEnemy)
        {
            this.target = base.transform;
        }
        else
        {
            this.target = Object.FindObjectOfType<PlayerController>().transform;
        }
        if (!this.soundOnStart)
        {
            this.currentTimeBetweenSounds = this.NewSoundCooldown();
        }
    }

    // Token: 0x060001D4 RID: 468 RVA: 0x00008EC0 File Offset: 0x000070C0
    public void Update()
    {
        if (this.soundEffects.Length != 0)
        {
            if (this.currentTimeBetweenSounds <= 0f)
            {
                Object.Instantiate<GameObject>(this.soundEffects[Random.Range(0, this.soundEffects.Length)], this.target.position, Quaternion.identity);
                this.currentTimeBetweenSounds = this.NewSoundCooldown();
                return;
            }
            this.currentTimeBetweenSounds -= Time.deltaTime;
        }
    }

    // Token: 0x060001D5 RID: 469 RVA: 0x00008F2D File Offset: 0x0000712D
    public float NewSoundCooldown()
    {
        return Random.Range(this.minSecondsBetweenSounds, this.maxSecondsBetweenSounds);
    }

    // Token: 0x04000243 RID: 579
    public GameObject[] soundEffects;

    // Token: 0x04000244 RID: 580
    public float minSecondsBetweenSounds;

    // Token: 0x04000245 RID: 581
    public float maxSecondsBetweenSounds;

    // Token: 0x04000246 RID: 582
    public float currentTimeBetweenSounds;

    // Token: 0x04000247 RID: 583
    public bool soundOnStart;

    // Token: 0x04000248 RID: 584
    public bool isEnemy;

    // Token: 0x04000249 RID: 585
    private Transform target;
}

using System;
using UnityEngine;

// Token: 0x02000058 RID: 88
public class SoundOnDestroy : MonoBehaviour
{
    // Token: 0x060001CA RID: 458 RVA: 0x00008D88 File Offset: 0x00006F88
    private void OnDestroy()
    {
        Object.Instantiate<GameObject>(this.sound, base.transform.position, Quaternion.identity);
    }

    // Token: 0x0400023D RID: 573
    public GameObject sound;
}

using System;
using UnityEngine;

// Token: 0x0200006E RID: 110
public class SpaceDoor : MonoBehaviour
{
    // Token: 0x06000229 RID: 553 RVA: 0x0000A7EF File Offset: 0x000089EF
    private void Start()
    {
        this._animator = base.GetComponent<Animator>();
    }

    // Token: 0x0600022A RID: 554 RVA: 0x0000A7FD File Offset: 0x000089FD
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && this.itemCollector.items >= this.itemsToEnter)
        {
            this._animator.SetBool("isOpened", true);
        }
    }

    // Token: 0x0600022B RID: 555 RVA: 0x0000A830 File Offset: 0x00008A30
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this._animator.SetBool("isOpened", false);
        }
    }

    // Token: 0x0400029C RID: 668
    [SerializeField]
    private ItemCollector itemCollector;

    // Token: 0x0400029D RID: 669
    [SerializeField]
    private int itemsToEnter;

    // Token: 0x0400029E RID: 670
    private Animator _animator;
}

using System;
using UnityEngine;

// Token: 0x0200006C RID: 108
public class SparksBullet : MonoBehaviour
{
    // Token: 0x06000222 RID: 546 RVA: 0x0000A5FC File Offset: 0x000087FC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            if (other.transform.root.GetComponent<Enemy>() != null)
            {
                other.transform.root.GetComponent<Enemy>().TakeDamage(this.bulletDamage);
                Object.Destroy(base.gameObject);
                return;
            }
            if (other.transform.root.GetComponent<MushroomHunter>() != null)
            {
                other.transform.root.GetComponent<MushroomHunter>().TakeDamage(this.bulletDamage);
                Object.Destroy(base.gameObject);
                return;
            }
            if (other.transform.root.GetComponent<Boss>() != null)
            {
                other.transform.root.GetComponent<Boss>().TakeDamage(this.bulletDamage);
                Object.Destroy(base.gameObject);
                return;
            }
        }
        else
        {
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x06000223 RID: 547 RVA: 0x0000A6E1 File Offset: 0x000088E1
    private void OnDestroy()
    {
        Object.Instantiate<GameObject>(this.fireBall, base.transform.position, Quaternion.identity);
    }

    // Token: 0x04000293 RID: 659
    public int bulletDamage;

    // Token: 0x04000294 RID: 660
    public GameObject fireBall;
}

using System;
using UnityEngine;

// Token: 0x02000073 RID: 115
public class SpawnEffect : MonoBehaviour
{
    // Token: 0x06000238 RID: 568 RVA: 0x0000AA10 File Offset: 0x00008C10
    private void Start()
    {
        this.shaderProperty = Shader.PropertyToID("_cutoff");
        this._renderer = base.GetComponent<Renderer>();
        this.ps = base.GetComponentInChildren<ParticleSystem>();
        this.ps.main.duration = this.spawnEffectTime;
        this.ps.Play();
    }

    // Token: 0x06000239 RID: 569 RVA: 0x0000AA6C File Offset: 0x00008C6C
    private void Update()
    {
        if (this.timer < this.spawnEffectTime + this.pause)
        {
            this.timer += Time.deltaTime;
        }
        else
        {
            this.ps.Play();
            this.timer = 0f;
        }
        this._renderer.material.SetFloat(this.shaderProperty, this.fadeIn.Evaluate(Mathf.InverseLerp(0f, this.spawnEffectTime, this.timer)));
    }

    // Token: 0x040002A8 RID: 680
    public float spawnEffectTime = 2f;

    // Token: 0x040002A9 RID: 681
    public float pause = 1f;

    // Token: 0x040002AA RID: 682
    public AnimationCurve fadeIn;

    // Token: 0x040002AB RID: 683
    private ParticleSystem ps;

    // Token: 0x040002AC RID: 684
    private float timer;

    // Token: 0x040002AD RID: 685
    private Renderer _renderer;

    // Token: 0x040002AE RID: 686
    private int shaderProperty;
}

using System;
using System.Collections.Generic;
using System.Reflection;

// Token: 0x02000078 RID: 120
public static class SubclassFinder
{
    // Token: 0x06000244 RID: 580 RVA: 0x0000AE48 File Offset: 0x00009048
    public static List<Type> GetSubclasses<T>()
    {
        List<Type> list = new List<Type>();
        Type typeFromHandle = typeof(T);
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            foreach (Type type in assemblies[i].GetTypes())
            {
                if (type.IsSubclassOf(typeFromHandle) || type == typeFromHandle)
                {
                    list.Add(type);
                }
            }
        }
        return list;
    }
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x0200006F RID: 111
public class TextChanger : MonoBehaviour
{
    // Token: 0x0600022D RID: 557 RVA: 0x0000A858 File Offset: 0x00008A58
    public void CallNextLine()
    {
        this.textUI.text = this.textLines[this.lineNumber];
    }

    // Token: 0x0400029F RID: 671
    public TextMeshProUGUI textUI;

    // Token: 0x040002A0 RID: 672
    public string[] textLines;

    // Token: 0x040002A1 RID: 673
    public int lineNumber;
}

using System;
using UnityEngine;

// Token: 0x02000072 RID: 114
public class TextRotator : MonoBehaviour
{
    // Token: 0x06000236 RID: 566 RVA: 0x0000A9C8 File Offset: 0x00008BC8
    private void Update()
    {
        Vector3 position = Camera.main.transform.position;
        position.y = base.transform.position.y;
        base.transform.LookAt(position);
    }
}

using System;
using UnityEngine;

// Token: 0x02000056 RID: 86
public class ThrowingTutorial : MonoBehaviour
{
    // Token: 0x060001C1 RID: 449 RVA: 0x00008C21 File Offset: 0x00006E21
    private void Start()
    {
        this.readyToThrow = true;
    }

    // Token: 0x060001C2 RID: 450 RVA: 0x00008C2A File Offset: 0x00006E2A
    private void Update()
    {
        if (this.readyToThrow && this.totalThrows > 0)
        {
            this.Throw();
        }
    }

    // Token: 0x060001C3 RID: 451 RVA: 0x00008C44 File Offset: 0x00006E44
    private void Throw()
    {
        this.readyToThrow = false;
        Rigidbody component = Object.Instantiate<GameObject>(this.objectToThrow, this.attackPoint.position, this.attackPoint.rotation).GetComponent<Rigidbody>();
        Vector3 a = this.attackPoint.transform.forward;
        RaycastHit raycastHit;
        if (Physics.Raycast(this.attackPoint.position, this.attackPoint.forward, out raycastHit, 500f))
        {
            a = (raycastHit.point - this.attackPoint.position).normalized;
        }
        Vector3 force = a * this.throwForce + base.transform.up * this.throwUpwardForce;
        component.AddForce(force, ForceMode.Impulse);
        this.totalThrows--;
        base.Invoke("ResetThrow", this.throwCooldown);
    }

    // Token: 0x060001C4 RID: 452 RVA: 0x00008D21 File Offset: 0x00006F21
    private void ResetThrow()
    {
        this.readyToThrow = true;
    }

    // Token: 0x04000232 RID: 562
    [Header("References")]
    public Transform attackPoint;

    // Token: 0x04000233 RID: 563
    public GameObject objectToThrow;

    // Token: 0x04000234 RID: 564
    [Header("Settings")]
    public int totalThrows;

    // Token: 0x04000235 RID: 565
    public float throwCooldown;

    // Token: 0x04000236 RID: 566
    [Header("Throwing")]
    public KeyCode throwKey = KeyCode.Mouse0;

    // Token: 0x04000237 RID: 567
    public float throwForce;

    // Token: 0x04000238 RID: 568
    public float throwUpwardForce;

    // Token: 0x04000239 RID: 569
    private bool readyToThrow;
}

using System;
using UnityEngine;
using UnityEngine.Events;

// Token: 0x02000076 RID: 118
public class TriggerArea : MonoBehaviour
{
    // Token: 0x06000241 RID: 577 RVA: 0x0000AE23 File Offset: 0x00009023
    private void OnTriggerEnter(Collider other)
    {
        this.OnEnter.Invoke();
    }

    // Token: 0x06000242 RID: 578 RVA: 0x0000AE30 File Offset: 0x00009030
    private void OnTriggerExit(Collider other)
    {
        this.OnExit.Invoke();
    }

    // Token: 0x040002BE RID: 702
    public UnityEvent OnEnter;

    // Token: 0x040002BF RID: 703
    public UnityEvent OnExit;
}

using System;
using UnityEngine;

// Token: 0x02000042 RID: 66
public class TrollBullet : MonoBehaviour
{
    // Token: 0x06000161 RID: 353 RVA: 0x00006E6D File Offset: 0x0000506D
    private void Start()
    {
        this.direction = Vector3.forward;
        this.sable = GameObject.FindGameObjectWithTag("Sable").transform.parent.GetComponent<Animator>();
    }

    // Token: 0x06000162 RID: 354 RVA: 0x00006E99 File Offset: 0x00005099
    private void Update()
    {
        base.transform.Translate(this.direction * this.speed * Time.deltaTime);
    }

    // Token: 0x06000163 RID: 355 RVA: 0x00006EC4 File Offset: 0x000050C4
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.GetComponent<PlayerHealth>() != null)
        {
            other.transform.root.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            Object.Destroy(base.gameObject);
            return;
        }
        if (other.CompareTag("Sable") && !this.sable.GetCurrentAnimatorStateInfo(0).IsName("idle"))
        {
            this.direction = -this.direction;
            return;
        }
        if (other.transform.root.name == "EnemyTroll")
        {
            other.transform.root.GetComponent<Enemy>().TakeDamage(this.damageForEnemy);
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x06000164 RID: 356 RVA: 0x00006F8D File Offset: 0x0000518D
    private void OnDestroy()
    {
        Object.Instantiate<GameObject>(this.fireBall, base.transform.position, Quaternion.identity);
    }

    // Token: 0x040001A0 RID: 416
    public int damage;

    // Token: 0x040001A1 RID: 417
    public int damageForEnemy;

    // Token: 0x040001A2 RID: 418
    public float speed;

    // Token: 0x040001A3 RID: 419
    public GameObject fireBall;

    // Token: 0x040001A4 RID: 420
    public Animator sable;

    // Token: 0x040001A5 RID: 421
    private Vector3 direction;
}

using System;
using UnityEngine;

// Token: 0x0200005C RID: 92
public class TrollThrower : MonoBehaviour
{
    // Token: 0x060001D7 RID: 471 RVA: 0x00008F48 File Offset: 0x00007148
    private void Start()
    {
        this.bulletTime = (float)this.startBulletTime;
    }

    // Token: 0x060001D8 RID: 472 RVA: 0x00008F57 File Offset: 0x00007157
    private void Update()
    {
        this.bulletTime -= Time.deltaTime;
        if (this.bulletTime <= 0f)
        {
            this.SpawnSparks();
            this.bulletTime = (float)this.startBulletTime;
        }
    }

    // Token: 0x060001D9 RID: 473 RVA: 0x00008F8C File Offset: 0x0000718C
    public void SpawnSparks()
    {
        this.bulletPos.LookAt(this.player);
        Object.Instantiate<GameObject>(this.bullet, this.bulletPos.position, this.bulletPos.rotation);
        Object.Instantiate<GameObject>(this.sounds[Random.Range(0, this.sounds.Length)], this.bulletPos.position, Quaternion.identity);
    }

    // Token: 0x0400024A RID: 586
    public GameObject bullet;

    // Token: 0x0400024B RID: 587
    public Transform bulletPos;

    // Token: 0x0400024C RID: 588
    public GameObject[] sounds;

    // Token: 0x0400024D RID: 589
    public Transform player;

    // Token: 0x0400024E RID: 590
    public int startBulletTime;

    // Token: 0x0400024F RID: 591
    private float bulletTime;
}

using System;
using UnityEngine;

// Token: 0x02000043 RID: 67
public class Trotil : MonoBehaviour
{
    // Token: 0x06000166 RID: 358 RVA: 0x00006FB3 File Offset: 0x000051B3
    private void Start()
    {
        base.GetComponent<Rigidbody>().AddForce(this.startPush + base.transform.forward * 8f, ForceMode.Impulse);
    }

    // Token: 0x06000167 RID: 359 RVA: 0x00006FE1 File Offset: 0x000051E1
    public void OnExploded()
    {
        Object.Instantiate<GameObject>(this.explosion, base.transform.position, Quaternion.identity);
        Object.Destroy(base.transform.parent.gameObject);
    }

    // Token: 0x040001A6 RID: 422
    public GameObject explosion;

    // Token: 0x040001A7 RID: 423
    public Vector3 startPush;
}

using System;
using UnityEngine;
using UnityEngine.Video;

// Token: 0x0200005D RID: 93
public class TVBehaviour : MonoBehaviour
{
    // Token: 0x060001DB RID: 475 RVA: 0x00008FFF File Offset: 0x000071FF
    private void Start()
    {
        this.spriteRenderer = base.GetComponent<SpriteRenderer>();
    }

    // Token: 0x060001DC RID: 476 RVA: 0x0000900D File Offset: 0x0000720D
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.spriteRenderer.color = Color.white;
            this.clip.Play();
        }
    }

    // Token: 0x060001DD RID: 477 RVA: 0x00009037 File Offset: 0x00007237
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.spriteRenderer.color = Color.black;
            this.clip.Stop();
        }
    }

    // Token: 0x04000250 RID: 592
    private SpriteRenderer spriteRenderer;

    // Token: 0x04000251 RID: 593
    public VideoPlayer clip;
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Token: 0x02000014 RID: 20
public class VariableJoystick : Joystick
{
    // Token: 0x1700000A RID: 10
    // (get) Token: 0x06000055 RID: 85 RVA: 0x00002E78 File Offset: 0x00001078
    // (set) Token: 0x06000056 RID: 86 RVA: 0x00002E80 File Offset: 0x00001080
    public float MoveThreshold
    {
        get
        {
            return this.moveThreshold;
        }
        set
        {
            this.moveThreshold = Mathf.Abs(value);
        }
    }

    // Token: 0x06000057 RID: 87 RVA: 0x00002E8E File Offset: 0x0000108E
    public void SetMode(JoystickType joystickType)
    {
        this.joystickType = joystickType;
        if (joystickType == JoystickType.Fixed)
        {
            this.background.anchoredPosition = this.fixedPosition;
            this.background.gameObject.SetActive(true);
            return;
        }
        this.background.gameObject.SetActive(false);
    }

    // Token: 0x06000058 RID: 88 RVA: 0x00002ECE File Offset: 0x000010CE
    protected override void Start()
    {
        base.Start();
        this.fixedPosition = this.background.anchoredPosition;
        this.SetMode(this.joystickType);
    }

    // Token: 0x06000059 RID: 89 RVA: 0x00002EF3 File Offset: 0x000010F3
    public override void OnPointerDown(PointerEventData eventData)
    {
        if (this.joystickType != JoystickType.Fixed)
        {
            this.background.anchoredPosition = base.ScreenPointToAnchoredPosition(eventData.position);
            this.background.gameObject.SetActive(true);
        }
        base.OnPointerDown(eventData);
    }

    // Token: 0x0600005A RID: 90 RVA: 0x00002F2C File Offset: 0x0000112C
    public override void OnPointerUp(PointerEventData eventData)
    {
        if (this.joystickType != JoystickType.Fixed)
        {
            this.background.gameObject.SetActive(false);
        }
        base.OnPointerUp(eventData);
    }

    // Token: 0x0600005B RID: 91 RVA: 0x00002F50 File Offset: 0x00001150
    protected override void HandleInput(float magnitude, Vector2 normalised, Vector2 radius, Camera cam)
    {
        if (this.joystickType == JoystickType.Dynamic && magnitude > this.moveThreshold)
        {
            Vector2 b = normalised * (magnitude - this.moveThreshold) * radius;
            this.background.anchoredPosition += b;
        }
        base.HandleInput(magnitude, normalised, radius, cam);
    }

    // Token: 0x0400003D RID: 61
    [SerializeField]
    private float moveThreshold = 1f;

    // Token: 0x0400003E RID: 62
    [SerializeField]
    private JoystickType joystickType;

    // Token: 0x0400003F RID: 63
    private Vector2 fixedPosition = Vector2.zero;
}

using System;
using Ditzelgames;
using UnityEngine;

// Token: 0x02000044 RID: 68
[RequireComponent(typeof(WaterFloat))]
public class WaterBoat : MonoBehaviour
{
    // Token: 0x06000169 RID: 361 RVA: 0x0000701C File Offset: 0x0000521C
    public void Awake()
    {
        this.ParticleSystem = base.GetComponentInChildren<ParticleSystem>();
        this.Rigidbody = base.GetComponent<Rigidbody>();
        this.StartRotation = this.Motor.localRotation;
        this.Camera = Camera.main;
    }

    // Token: 0x0600016A RID: 362 RVA: 0x00007054 File Offset: 0x00005254
    public void FixedUpdate()
    {
        Vector3 forward = base.transform.forward;
        int num = 0;
        if (Input.GetKey(KeyCode.A))
        {
            num = 1;
        }
        if (Input.GetKey(KeyCode.D))
        {
            num = -1;
        }
        this.Rigidbody.AddForceAtPosition((float)num * base.transform.right * this.SteerPower / 100f, this.Motor.position);
        Vector3 a = Vector3.Scale(new Vector3(1f, 0f, 1f), base.transform.forward);
        Vector3 zero = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
        {
            PhysicsHelper.ApplyForceToReachVelocity(this.Rigidbody, a * this.MaxSpeed, this.Power, ForceMode.Force);
        }
        if (Input.GetKey(KeyCode.S))
        {
            PhysicsHelper.ApplyForceToReachVelocity(this.Rigidbody, a * -this.MaxSpeed, this.Power, ForceMode.Force);
        }
        this.Motor.SetPositionAndRotation(this.Motor.position, base.transform.rotation * this.StartRotation * Quaternion.Euler(0f, 30f * (float)num, 0f));
        if (this.ParticleSystem != null)
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S))
            {
                this.ParticleSystem.Play();
            }
            else
            {
                this.ParticleSystem.Pause();
            }
        }
        bool flag = Vector3.Cross(base.transform.forward, this.Rigidbody.velocity).y < 0f;
        this.Rigidbody.velocity = Quaternion.AngleAxis(Vector3.SignedAngle(this.Rigidbody.velocity, (flag ? 1f : 0f) * base.transform.forward, Vector3.up) * this.Drag, Vector3.up) * this.Rigidbody.velocity;
    }

    // Token: 0x040001A8 RID: 424
    public Transform Motor;

    // Token: 0x040001A9 RID: 425
    public float SteerPower = 500f;

    // Token: 0x040001AA RID: 426
    public float Power = 5f;

    // Token: 0x040001AB RID: 427
    public float MaxSpeed = 10f;

    // Token: 0x040001AC RID: 428
    public float Drag = 0.1f;

    // Token: 0x040001AD RID: 429
    protected Rigidbody Rigidbody;

    // Token: 0x040001AE RID: 430
    protected Quaternion StartRotation;

    // Token: 0x040001AF RID: 431
    protected ParticleSystem ParticleSystem;

    // Token: 0x040001B0 RID: 432
    protected Camera Camera;

    // Token: 0x040001B1 RID: 433
    protected Vector3 CamVel;
}

using System;
using Ditzelgames;
using UnityEngine;

// Token: 0x02000065 RID: 101
[RequireComponent(typeof(Rigidbody))]
public class WaterFloat : MonoBehaviour
{
    // Token: 0x17000010 RID: 16
    // (get) Token: 0x060001FE RID: 510 RVA: 0x0000945A File Offset: 0x0000765A
    public Vector3 Center
    {
        get
        {
            return base.transform.position + this.centerOffset;
        }
    }

    // Token: 0x060001FF RID: 511 RVA: 0x00009474 File Offset: 0x00007674
    private void Awake()
    {
        this.Waves = Object.FindObjectOfType<Waves>();
        this.Rigidbody = base.GetComponent<Rigidbody>();
        this.Rigidbody.useGravity = false;
        this.WaterLinePoints = new Vector3[this.FloatPoints.Length];
        for (int i = 0; i < this.FloatPoints.Length; i++)
        {
            this.WaterLinePoints[i] = this.FloatPoints[i].position;
        }
        this.centerOffset = PhysicsHelper.GetCenter(this.WaterLinePoints) - base.transform.position;
    }

    // Token: 0x06000200 RID: 512 RVA: 0x00009504 File Offset: 0x00007704
    private void FixedUpdate()
    {
        float num = 0f;
        bool flag = false;
        for (int i = 0; i < this.FloatPoints.Length; i++)
        {
            this.WaterLinePoints[i] = this.FloatPoints[i].position;
            this.WaterLinePoints[i].y = this.Waves.GetHeight(this.FloatPoints[i].position);
            num += this.WaterLinePoints[i].y / (float)this.FloatPoints.Length;
            if (this.WaterLinePoints[i].y > this.FloatPoints[i].position.y)
            {
                flag = true;
            }
        }
        float d = num - this.WaterLine;
        this.WaterLine = num;
        this.TargetUp = PhysicsHelper.GetNormal(this.WaterLinePoints);
        Vector3 a = Physics.gravity;
        this.Rigidbody.drag = this.AirDrag;
        if (this.WaterLine > this.Center.y)
        {
            this.Rigidbody.drag = this.WaterDrag;
            if (this.AttachToSurface)
            {
                this.Rigidbody.position = new Vector3(this.Rigidbody.position.x, this.WaterLine - this.centerOffset.y, this.Rigidbody.position.z);
            }
            else
            {
                a = (this.AffectDirection ? (this.TargetUp * -Physics.gravity.y) : (-Physics.gravity));
                base.transform.Translate(Vector3.up * d * 0.9f);
            }
        }
        this.Rigidbody.AddForce(a * Mathf.Clamp(Mathf.Abs(this.WaterLine - this.Center.y), 0f, 1f));
        if (flag)
        {
            this.TargetUp = Vector3.SmoothDamp(base.transform.up, this.TargetUp, ref this.smoothVectorRotation, 0.2f);
            this.Rigidbody.rotation = Quaternion.FromToRotation(base.transform.up, this.TargetUp) * this.Rigidbody.rotation;
        }
    }

    // Token: 0x06000201 RID: 513 RVA: 0x0000974C File Offset: 0x0000794C
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        if (this.FloatPoints == null)
        {
            return;
        }
        for (int i = 0; i < this.FloatPoints.Length; i++)
        {
            if (!(this.FloatPoints[i] == null))
            {
                if (this.Waves != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawCube(this.WaterLinePoints[i], Vector3.one * 0.3f);
                }
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(this.FloatPoints[i].position, 0.1f);
            }
        }
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawCube(new Vector3(this.Center.x, this.WaterLine, this.Center.z), Vector3.one * 1f);
            Gizmos.DrawRay(new Vector3(this.Center.x, this.WaterLine, this.Center.z), this.TargetUp * 1f);
        }
    }

    // Token: 0x04000265 RID: 613
    public float AirDrag = 1f;

    // Token: 0x04000266 RID: 614
    public float WaterDrag = 10f;

    // Token: 0x04000267 RID: 615
    public bool AffectDirection = true;

    // Token: 0x04000268 RID: 616
    public bool AttachToSurface;

    // Token: 0x04000269 RID: 617
    public Transform[] FloatPoints;

    // Token: 0x0400026A RID: 618
    protected Rigidbody Rigidbody;

    // Token: 0x0400026B RID: 619
    protected Waves Waves;

    // Token: 0x0400026C RID: 620
    protected float WaterLine;

    // Token: 0x0400026D RID: 621
    protected Vector3[] WaterLinePoints;

    // Token: 0x0400026E RID: 622
    protected Vector3 smoothVectorRotation;

    // Token: 0x0400026F RID: 623
    protected Vector3 TargetUp;

    // Token: 0x04000270 RID: 624
    protected Vector3 centerOffset;
}

using System;
using UnityEngine;

// Token: 0x02000066 RID: 102
public class Waves : MonoBehaviour
{
    // Token: 0x06000203 RID: 515 RVA: 0x0000988C File Offset: 0x00007A8C
    private void Start()
    {
        this.Mesh = new Mesh();
        this.Mesh.name = base.gameObject.name;
        this.Mesh.vertices = this.GenerateVerts();
        this.Mesh.triangles = this.GenerateTries();
        this.Mesh.uv = this.GenerateUVs();
        this.Mesh.RecalculateNormals();
        this.Mesh.RecalculateBounds();
        this.MeshFilter = base.gameObject.AddComponent<MeshFilter>();
        this.MeshFilter.mesh = this.Mesh;
    }

    // Token: 0x06000204 RID: 516 RVA: 0x00009928 File Offset: 0x00007B28
    public float GetHeight(Vector3 position)
    {
        Vector3 b = new Vector3(1f / base.transform.lossyScale.x, 0f, 1f / base.transform.lossyScale.z);
        Vector3 vector = Vector3.Scale(position - base.transform.position, b);
        Vector3 vector2 = new Vector3(Mathf.Floor(vector.x), 0f, Mathf.Floor(vector.z));
        Vector3 vector3 = new Vector3(Mathf.Floor(vector.x), 0f, Mathf.Ceil(vector.z));
        Vector3 vector4 = new Vector3(Mathf.Ceil(vector.x), 0f, Mathf.Floor(vector.z));
        Vector3 vector5 = new Vector3(Mathf.Ceil(vector.x), 0f, Mathf.Ceil(vector.z));
        vector2.x = Mathf.Clamp(vector2.x, 0f, (float)this.Dimension);
        vector2.z = Mathf.Clamp(vector2.z, 0f, (float)this.Dimension);
        vector3.x = Mathf.Clamp(vector3.x, 0f, (float)this.Dimension);
        vector3.z = Mathf.Clamp(vector3.z, 0f, (float)this.Dimension);
        vector4.x = Mathf.Clamp(vector4.x, 0f, (float)this.Dimension);
        vector4.z = Mathf.Clamp(vector4.z, 0f, (float)this.Dimension);
        vector5.x = Mathf.Clamp(vector5.x, 0f, (float)this.Dimension);
        vector5.z = Mathf.Clamp(vector5.z, 0f, (float)this.Dimension);
        float num = Mathf.Max(new float[]
        {
            Vector3.Distance(vector2, vector),
            Vector3.Distance(vector3, vector),
            Vector3.Distance(vector4, vector),
            Vector3.Distance(vector5, vector) + Mathf.Epsilon
        });
        float num2 = num - Vector3.Distance(vector2, vector) + (num - Vector3.Distance(vector3, vector)) + (num - Vector3.Distance(vector4, vector)) + (num - Vector3.Distance(vector5, vector) + Mathf.Epsilon);
        return (this.Mesh.vertices[this.index(vector2.x, vector2.z)].y * (num - Vector3.Distance(vector2, vector)) + this.Mesh.vertices[this.index(vector3.x, vector3.z)].y * (num - Vector3.Distance(vector3, vector)) + this.Mesh.vertices[this.index(vector4.x, vector4.z)].y * (num - Vector3.Distance(vector4, vector)) + this.Mesh.vertices[this.index(vector5.x, vector5.z)].y * (num - Vector3.Distance(vector5, vector))) * base.transform.lossyScale.y / num2;
    }

    // Token: 0x06000205 RID: 517 RVA: 0x00009C58 File Offset: 0x00007E58
    private Vector3[] GenerateVerts()
    {
        Vector3[] array = new Vector3[(this.Dimension + 1) * (this.Dimension + 1)];
        for (int i = 0; i <= this.Dimension; i++)
        {
            for (int j = 0; j <= this.Dimension; j++)
            {
                array[this.index(i, j)] = new Vector3((float)i, 0f, (float)j);
            }
        }
        return array;
    }

    // Token: 0x06000206 RID: 518 RVA: 0x00009CBC File Offset: 0x00007EBC
    private int[] GenerateTries()
    {
        int[] array = new int[this.Mesh.vertices.Length * 6];
        for (int i = 0; i < this.Dimension; i++)
        {
            for (int j = 0; j < this.Dimension; j++)
            {
                array[this.index(i, j) * 6] = this.index(i, j);
                array[this.index(i, j) * 6 + 1] = this.index(i + 1, j + 1);
                array[this.index(i, j) * 6 + 2] = this.index(i + 1, j);
                array[this.index(i, j) * 6 + 3] = this.index(i, j);
                array[this.index(i, j) * 6 + 4] = this.index(i, j + 1);
                array[this.index(i, j) * 6 + 5] = this.index(i + 1, j + 1);
            }
        }
        return array;
    }

    // Token: 0x06000207 RID: 519 RVA: 0x00009D9C File Offset: 0x00007F9C
    private Vector2[] GenerateUVs()
    {
        Vector2[] array = new Vector2[this.Mesh.vertices.Length];
        for (int i = 0; i <= this.Dimension; i++)
        {
            for (int j = 0; j <= this.Dimension; j++)
            {
                Vector2 vector = new Vector2((float)i / this.UVScale % 2f, (float)j / this.UVScale % 2f);
                array[this.index(i, j)] = new Vector2((vector.x <= 1f) ? vector.x : (2f - vector.x), (vector.y <= 1f) ? vector.y : (2f - vector.y));
            }
        }
        return array;
    }

    // Token: 0x06000208 RID: 520 RVA: 0x00009E62 File Offset: 0x00008062
    private int index(int x, int z)
    {
        return x * (this.Dimension + 1) + z;
    }

    // Token: 0x06000209 RID: 521 RVA: 0x00009E70 File Offset: 0x00008070
    private int index(float x, float z)
    {
        return this.index((int)x, (int)z);
    }

    // Token: 0x0600020A RID: 522 RVA: 0x00009E7C File Offset: 0x0000807C
    private void Update()
    {
        Vector3[] vertices = this.Mesh.vertices;
        for (int i = 0; i <= this.Dimension; i++)
        {
            for (int j = 0; j <= this.Dimension; j++)
            {
                float num = 0f;
                for (int k = 0; k < this.Octaves.Length; k++)
                {
                    if (this.Octaves[k].alternate)
                    {
                        float num2 = Mathf.PerlinNoise((float)i * this.Octaves[k].scale.x / (float)this.Dimension, (float)j * this.Octaves[k].scale.y / (float)this.Dimension) * 3.1415927f * 2f;
                        num += Mathf.Cos(num2 + this.Octaves[k].speed.magnitude * Time.time) * this.Octaves[k].height;
                    }
                    else
                    {
                        float num3 = Mathf.PerlinNoise(((float)i * this.Octaves[k].scale.x + Time.time * this.Octaves[k].speed.x) / (float)this.Dimension, ((float)j * this.Octaves[k].scale.y + Time.time * this.Octaves[k].speed.y) / (float)this.Dimension) - 0.5f;
                        num += num3 * this.Octaves[k].height;
                    }
                }
                vertices[this.index(i, j)] = new Vector3((float)i, num, (float)j);
            }
        }
        this.Mesh.vertices = vertices;
        this.Mesh.RecalculateNormals();
    }

    // Token: 0x04000271 RID: 625
    public int Dimension = 10;

    // Token: 0x04000272 RID: 626
    public float UVScale = 2f;

    // Token: 0x04000273 RID: 627
    public Waves.Octave[] Octaves;

    // Token: 0x04000274 RID: 628
    protected MeshFilter MeshFilter;

    // Token: 0x04000275 RID: 629
    protected Mesh Mesh;

    // Token: 0x020000D3 RID: 211
    [Serializable]
    public struct Octave
    {
        // Token: 0x0400048A RID: 1162
        public Vector2 speed;

        // Token: 0x0400048B RID: 1163
        public Vector2 scale;

        // Token: 0x0400048C RID: 1164
        public float height;

        // Token: 0x0400048D RID: 1165
        public bool alternate;
    }
}

using System;
using UnityEngine;

// Token: 0x0200006D RID: 109
public class WeaponController : MonoBehaviour
{
    // Token: 0x06000225 RID: 549 RVA: 0x0000A707 File Offset: 0x00008907
    private void Start()
    {
        this.weaponAnimator = base.GetComponent<Animator>();
    }

    // Token: 0x06000226 RID: 550 RVA: 0x0000A718 File Offset: 0x00008918
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            this.weaponAnimator.SetTrigger("isAttacking");
        }
        if (this.newSparks != null)
        {
            this.newSparks.transform.Translate(this.sparksDirection * (float)this.sparksSpeed * Time.deltaTime);
        }
    }

    // Token: 0x06000227 RID: 551 RVA: 0x0000A778 File Offset: 0x00008978
    public void SpawnSparks()
    {
        this.newSparks = Object.Instantiate<GameObject>(this.sparksEffect, this.sparksPos.position, Quaternion.identity);
        this.sparksDirection = this.sparksPos.transform.forward;
        Object.Instantiate<GameObject>(this.sounds[Random.Range(0, this.sounds.Length)], this.sparksPos.position, Quaternion.identity);
    }

    // Token: 0x04000295 RID: 661
    public GameObject sparksEffect;

    // Token: 0x04000296 RID: 662
    public GameObject newSparks;

    // Token: 0x04000297 RID: 663
    public Transform sparksPos;

    // Token: 0x04000298 RID: 664
    public int sparksSpeed;

    // Token: 0x04000299 RID: 665
    public GameObject[] sounds;

    // Token: 0x0400029A RID: 666
    private Animator weaponAnimator;

    // Token: 0x0400029B RID: 667
    private Vector3 sparksDirection;
}

using System;
using UnityEngine;

// Token: 0x02000064 RID: 100
public class YT : MonoBehaviour
{
    // Token: 0x060001FB RID: 507 RVA: 0x0000943A File Offset: 0x0000763A
    public void OnButtonDown()
    {
        Application.OpenURL("https://www.youtube.com/channel/UCmjNGJn0WNPJS_G52UfkH-g");
    }

    // Token: 0x060001FC RID: 508 RVA: 0x00009446 File Offset: 0x00007646
    public void OnSteamButtonDown()
    {
        Application.OpenURL("https://store.steampowered.com/app/2477600/Offscreen/");
    }
}

// Бу! Испугался?

using System;

// Token: 0x02000001 RID: 1
internal class <Module>
{
}

using System;
using UnityEngine;

// Token: 0x02000028 RID: 40
public class AfterTitles : MonoBehaviour
{
    // Token: 0x060000E0 RID: 224 RVA: 0x00005178 File Offset: 0x00003378
    public void OnTitlesOver()
    {
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        if (this.spawner != null)
        {
            Object.Destroy(this.spawner);
        }
        PlayerPrefs.DeleteAll();
        CursorController.UnlockCursor();
        this.menu.OnExit();
    }

    // Token: 0x040000F6 RID: 246
    public MenuExit menu;

    // Token: 0x040000F7 RID: 247
    public GameObject spawner;
}

using System;

// Token: 0x02000079 RID: 121
[Serializable]
public enum AvatarPosition
{
    // Token: 0x040002C4 RID: 708
    None,
    // Token: 0x040002C5 RID: 709
    Left,
    // Token: 0x040002C6 RID: 710
    Right
}

using System;

// Token: 0x0200007A RID: 122
[Serializable]
public enum AvatarType
{
    // Token: 0x040002C8 RID: 712
    Normal,
    // Token: 0x040002C9 RID: 713
    Smile,
    // Token: 0x040002CA RID: 714
    Suprized,
    // Token: 0x040002CB RID: 715
    Disgust,
    // Token: 0x040002CC RID: 716
    Crying,
    // Token: 0x040002CD RID: 717
    Angry
}

using System;

// Token: 0x02000010 RID: 16
public enum AxisOptions
{
    // Token: 0x04000039 RID: 57
    Both,
    // Token: 0x0400003A RID: 58
    Horizontal,
    // Token: 0x0400003B RID: 59
    Vertical
}

using System;
using System.Collections;
using UnityEngine;

// Token: 0x02000002 RID: 2
public class BarrierDamageDealer : MonoBehaviour
{
    // Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.CompareTag("Player"))
        {
            this.playerInArea = true;
            this.warningText.SetActive(true);
            if (this.damageCoroutine == null)
            {
                this.damageCoroutine = base.StartCoroutine(this.ApplyDamage(other.transform.root.GetComponent<PlayerHealth>()));
            }
        }
    }

    // Token: 0x06000002 RID: 2 RVA: 0x000020B4 File Offset: 0x000002B4
    private void OnTriggerExit(Collider other)
    {
        if (other.transform.root.CompareTag("Player"))
        {
            this.playerInArea = false;
            this.warningText.SetActive(false);
            if (this.damageCoroutine != null)
            {
                base.StopCoroutine(this.damageCoroutine);
                this.damageCoroutine = null;
            }
        }
    }

    // Token: 0x06000003 RID: 3 RVA: 0x00002106 File Offset: 0x00000306
    private IEnumerator ApplyDamage(PlayerHealth player)
    {
        while (this.playerInArea)
        {
            if (player != null)
            {
                player.TakeDamage((float)this.damagePerSecond);
            }
            yield return new WaitForSeconds(this.damageInterval);
        }
        this.damageCoroutine = null;
        yield break;
    }

    // Token: 0x04000001 RID: 1
    public GameObject warningText;

    // Token: 0x04000002 RID: 2
    public int damagePerSecond = 10;

    // Token: 0x04000003 RID: 3
    private float damageInterval = 1f;

    // Token: 0x04000004 RID: 4
    private bool playerInArea;

    // Token: 0x04000005 RID: 5
    private Coroutine damageCoroutine;
}

using System;
using UnityEngine;

// Token: 0x02000020 RID: 32
public class BasicEnemy : MonoBehaviour
{
    // Token: 0x06000085 RID: 133 RVA: 0x000037DD File Offset: 0x000019DD
    public void TakeDamage(int damage)
    {
        this.health -= damage;
        if (this.health <= 0)
        {
            this.DestroyEnemy();
        }
    }

    // Token: 0x06000086 RID: 134 RVA: 0x000037FC File Offset: 0x000019FC
    public void DestroyEnemy()
    {
        Object.Destroy(base.gameObject);
    }

    // Token: 0x04000070 RID: 112
    [Header("Stats")]
    public int health;
}

using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// Token: 0x02000038 RID: 56
public class BeastBehaviour : MonoBehaviour
{
    // Token: 0x0600012E RID: 302 RVA: 0x00006371 File Offset: 0x00004571
    private void Start()
    {
        this.animator = base.GetComponent<Animator>();
        this.agent = base.GetComponent<NavMeshAgent>();
        this.sphereCollider = base.GetComponent<SphereCollider>();
    }

    // Token: 0x0600012F RID: 303 RVA: 0x00006397 File Offset: 0x00004597
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.OnEnterArea();
        }
    }

    // Token: 0x06000130 RID: 304 RVA: 0x000063AC File Offset: 0x000045AC
    public void OnEnterArea()
    {
        if (this.firstTime)
        {
            this.sphereCollider.enabled = false;
            this.firstTime = false;
        }
        this.dialogues[this.dialogueNum].Invoke();
        if (this.dialogueNum == 1)
        {
            GameObject[] array = GameObject.FindGameObjectsWithTag("Trotil");
            for (int i = 0; i < array.Length; i++)
            {
                Object.Destroy(array[i]);
            }
        }
        this.isCheckingEnd = true;
    }

    // Token: 0x06000131 RID: 305 RVA: 0x00006417 File Offset: 0x00004617
    private void Update()
    {
        if (this.isCheckingEnd)
        {
            this.CheckDialogueEnd();
        }
    }

    // Token: 0x06000132 RID: 306 RVA: 0x00006428 File Offset: 0x00004628
    public void CheckDialogueEnd()
    {
        if (!this.UI.activeInHierarchy)
        {
            this.isCheckingEnd = false;
            if (this.dialogueNum == 0)
            {
                base.Invoke("StartFight", 0.8f);
            }
            if (this.dialogueNum == 1)
            {
                base.Invoke("DisappearCompletely", 0.8f);
            }
        }
    }

    // Token: 0x06000133 RID: 307 RVA: 0x0000647A File Offset: 0x0000467A
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.skip.Invoke();
        }
    }

    // Token: 0x06000134 RID: 308 RVA: 0x00006494 File Offset: 0x00004694
    private void StartFight()
    {
        this.pier1.presence = "true";
        this.pier2.presence = "true";
        this.pier1.SavePresence();
        this.pier2.SavePresence();
        this.animator.SetBool("hasIncreased", true);
        this.agent.speed = 3.3f;
        this.trotilSpawner.SetActive(true);
        this.afterLab.SetActive(true);
        this.barrier.SetActive(false);
        Object.FindObjectOfType<MusicManager>();
    }

    // Token: 0x06000135 RID: 309 RVA: 0x00006524 File Offset: 0x00004724
    public void OnBoatEntered()
    {
        this.agent.enabled = false;
        this.sphereCollider.enabled = true;
        this.trap.SetActive(true);
        this.dialogueNum = 1;
        this.pier1.presence = "false";
        this.pier1.SavePresence();
        base.Invoke("ActivateTip", 1.7f);
        this.animator.SetBool("hasIncreased", false);
        this.agent.speed = 0f;
        Object.Instantiate<GameObject>(this.smoke, base.transform.position, Quaternion.identity);
        base.transform.position = this.finalPos.position;
        base.transform.rotation = this.finalPos.rotation;
        this.agent.enabled = true;
    }

    // Token: 0x06000136 RID: 310 RVA: 0x000065FC File Offset: 0x000047FC
    public void ActivateTip()
    {
        if (this.tip != null)
        {
            this.tip.SetActive(true);
        }
    }

    // Token: 0x06000137 RID: 311 RVA: 0x00006618 File Offset: 0x00004818
    public void DisappearCompletely()
    {
        Object.FindObjectOfType<MusicManager>();
        Object.Instantiate<GameObject>(this.smoke, base.transform.position, Quaternion.identity);
        this.flowerPile.SetActive(true);
        this.trap.SetActive(false);
        this.pier2.presence = "false";
        this.pier2.SavePresence();
        Object.Destroy(base.gameObject);
    }

    // Token: 0x04000161 RID: 353
    public bool firstTime;

    // Token: 0x04000162 RID: 354
    public bool isCheckingEnd;

    // Token: 0x04000163 RID: 355
    public GameObject UI;

    // Token: 0x04000164 RID: 356
    public GameObject trotilSpawner;

    // Token: 0x04000165 RID: 357
    public UnityEvent[] dialogues;

    // Token: 0x04000166 RID: 358
    public UnityEvent skip;

    // Token: 0x04000167 RID: 359
    public GameObject afterLab;

    // Token: 0x04000168 RID: 360
    public GameObject barrier;

    // Token: 0x04000169 RID: 361
    public GameObject tip;

    // Token: 0x0400016A RID: 362
    public GameObject smoke;

    // Token: 0x0400016B RID: 363
    public GameObject trap;

    // Token: 0x0400016C RID: 364
    public Pier pier1;

    // Token: 0x0400016D RID: 365
    public Pier pier2;

    // Token: 0x0400016E RID: 366
    public GameObject flowerPile;

    // Token: 0x0400016F RID: 367
    public Transform finalPos;

    // Token: 0x04000170 RID: 368
    public int dialogueNum;

    // Token: 0x04000171 RID: 369
    private Animator animator;

    // Token: 0x04000172 RID: 370
    private NavMeshAgent agent;

    // Token: 0x04000173 RID: 371
    private SphereCollider sphereCollider;
}

using System;
using Ditzelgames;
using UnityEngine;

// Token: 0x02000039 RID: 57
public class BoatController : MonoBehaviour
{
    // Token: 0x06000139 RID: 313 RVA: 0x0000668D File Offset: 0x0000488D
    private void Start()
    {
        this.ParticleSystem = base.GetComponentInChildren<ParticleSystem>();
        this.rb = base.GetComponent<Rigidbody>();
        this.StartRotation = this.motor.localRotation;
    }

    // Token: 0x0600013A RID: 314 RVA: 0x000066B8 File Offset: 0x000048B8
    private void FixedUpdate()
    {
        this.MoveBoat();
    }

    // Token: 0x0600013B RID: 315 RVA: 0x000066C0 File Offset: 0x000048C0
    private void MoveBoat()
    {
        Vector3 forward = base.transform.forward;
        int num = 0;
        if (Input.GetKey(KeyCode.A))
        {
            num = -1;
        }
        if (Input.GetKey(KeyCode.D))
        {
            num = 1;
        }
        this.rb.AddForceAtPosition((float)num * base.transform.right * this.steerPower / 100f, this.motor.position);
        Vector3 a = Vector3.Scale(new Vector3(1f, 0f, 1f), this.orientation.forward);
        Vector3 zero = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
        {
            PhysicsHelper.ApplyForceToReachVelocity(this.rb, a * this.speed, this.power, ForceMode.Force);
        }
        if (Input.GetKey(KeyCode.S))
        {
            PhysicsHelper.ApplyForceToReachVelocity(this.rb, a * -this.speed, this.power, ForceMode.Force);
        }
        this.motor.SetPositionAndRotation(this.motor.position, base.transform.rotation * this.StartRotation * Quaternion.Euler(0f, 30f * (float)num, 0f));
        if (this.ParticleSystem != null)
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S))
            {
                this.ParticleSystem.Play();
            }
            else
            {
                this.ParticleSystem.Pause();
            }
        }
        bool flag = Vector3.Cross(this.orientation.forward, this.rb.velocity).y < 0f;
        this.rb.velocity = Quaternion.AngleAxis(Vector3.SignedAngle(this.rb.velocity, (flag ? 1f : 0f) * base.transform.forward, Vector3.up) * this.drag, Vector3.up) * this.rb.velocity;
    }

    // Token: 0x04000174 RID: 372
    public Transform motor;

    // Token: 0x04000175 RID: 373
    public float speed;

    // Token: 0x04000176 RID: 374
    public float drag;

    // Token: 0x04000177 RID: 375
    public float power;

    // Token: 0x04000178 RID: 376
    public float steerPower = 200f;

    // Token: 0x04000179 RID: 377
    public Transform orientation;

    // Token: 0x0400017A RID: 378
    private float horizontalInput;

    // Token: 0x0400017B RID: 379
    private float verticalInput;

    // Token: 0x0400017C RID: 380
    private Vector3 moveDirection;

    // Token: 0x0400017D RID: 381
    private Rigidbody rb;

    // Token: 0x0400017E RID: 382
    protected Quaternion StartRotation;

    // Token: 0x0400017F RID: 383
    protected ParticleSystem ParticleSystem;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x0200004E RID: 78
public class BoatSwitch : MonoBehaviour
{
    // Token: 0x0600019D RID: 413 RVA: 0x00008016 File Offset: 0x00006216
    private void Update()
    {
        if (this.pierIsNear && Input.GetKeyDown(KeyCode.LeftShift))
        {
            this.ChangeBoatState();
        }
    }

    // Token: 0x0600019E RID: 414 RVA: 0x00008034 File Offset: 0x00006234
    private void ChangeBoatState()
    {
        if (this.player.enabled)
        {
            this.player.gameObject.SetActive(false);
            this.boat.enabled = true;
            this.player.enabled = false;
            this.boatText.text = "Shift - покинуть лодку";
            this.player.transform.position = this.seatPos.position;
            this.player.transform.parent = base.transform;
            this.cam.currentMode = MoveCamera.UpdateMode.FixedUpdate;
            this.player.GetComponent<Rigidbody>().isKinematic = true;
            if (this.trotil != null)
            {
                this.trotil.enabled = true;
                BeastBehaviour beastBehaviour = Object.FindObjectOfType<BeastBehaviour>();
                beastBehaviour.trotilSpawner.SetActive(false);
                beastBehaviour.OnBoatEntered();
            }
            this.player.gameObject.SetActive(true);
            return;
        }
        this.player.GetComponent<Rigidbody>().isKinematic = false;
        this.boat.enabled = false;
        this.player.enabled = true;
        this.boatText.text = "Shift - сесть в лодку";
        this.player.transform.position = this.pierPos.position;
        if (this.trotil != null)
        {
            this.trotil.enabled = false;
            BeastBehaviour beastBehaviour2 = Object.FindObjectOfType<BeastBehaviour>();
            if (beastBehaviour2 != null)
            {
                beastBehaviour2.OnEnterArea();
                beastBehaviour2.pier2.presence = "false";
                beastBehaviour2.pier2.SavePresence();
            }
        }
        this.player.transform.parent = null;
        this.cam.currentMode = MoveCamera.UpdateMode.LateUpdate;
    }

    // Token: 0x040001F2 RID: 498
    public PlayerController player;

    // Token: 0x040001F3 RID: 499
    public BoatController boat;

    // Token: 0x040001F4 RID: 500
    public Transform seatPos;

    // Token: 0x040001F5 RID: 501
    public Transform pierPos;

    // Token: 0x040001F6 RID: 502
    public MoveCamera cam;

    // Token: 0x040001F7 RID: 503
    public CurveSpawner trotil;

    // Token: 0x040001F8 RID: 504
    public TextMeshProUGUI boatText;

    // Token: 0x040001F9 RID: 505
    [HideInInspector]
    public bool pierIsNear;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Token: 0x02000021 RID: 33
public class BooBoss : MonoBehaviour
{
    // Token: 0x1700000D RID: 13
    // (get) Token: 0x06000088 RID: 136 RVA: 0x00003811 File Offset: 0x00001A11
    // (set) Token: 0x06000089 RID: 137 RVA: 0x00003819 File Offset: 0x00001A19
    public bool hasCompletedCutscene { get; set; }

    // Token: 0x1700000E RID: 14
    // (get) Token: 0x0600008A RID: 138 RVA: 0x00003822 File Offset: 0x00001A22
    // (set) Token: 0x0600008B RID: 139 RVA: 0x0000382A File Offset: 0x00001A2A
    public bool hasSeenTip { get; set; }

    // Token: 0x0600008C RID: 140 RVA: 0x00003833 File Offset: 0x00001A33
    private void Awake()
    {
        this.agent = base.GetComponent<NavMeshAgent>();
        this.animator = base.GetComponentInChildren<Animator>();
        this.health = this.startHealth;
    }

    // Token: 0x0600008D RID: 141 RVA: 0x0000385C File Offset: 0x00001A5C
    private void OnEnable()
    {
        CurveSpawner curveSpawner = this.phantomSpawner;
        curveSpawner.hasDoneSpawning = (Action)Delegate.Combine(curveSpawner.hasDoneSpawning, new Action(this.FinishPhantoms));
        CurveSpawner curveSpawner2 = this.phantomHardSpawner;
        curveSpawner2.hasDoneSpawning = (Action)Delegate.Combine(curveSpawner2.hasDoneSpawning, new Action(this.FinishPhantoms));
    }

    // Token: 0x0600008E RID: 142 RVA: 0x000038B8 File Offset: 0x00001AB8
    private void OnDisable()
    {
        CurveSpawner curveSpawner = this.phantomSpawner;
        curveSpawner.hasDoneSpawning = (Action)Delegate.Remove(curveSpawner.hasDoneSpawning, new Action(this.FinishPhantoms));
        CurveSpawner curveSpawner2 = this.phantomHardSpawner;
        curveSpawner2.hasDoneSpawning = (Action)Delegate.Remove(curveSpawner2.hasDoneSpawning, new Action(this.FinishPhantoms));
    }

    // Token: 0x0600008F RID: 143 RVA: 0x00003913 File Offset: 0x00001B13
    private void Start()
    {
        if (this.hasCompletedCutscene)
        {
            this.EnableBoss();
            return;
        }
        this.cutscene.SetActive(true);
    }

    // Token: 0x06000090 RID: 144 RVA: 0x00003930 File Offset: 0x00001B30
    public void EnableBoss()
    {
        this.hasCompletedCutscene = true;
        this.cutsceneChecker.MeetBooBoss();
        this.healthSlider.gameObject.SetActive(true);
        this.damage = this.startDamage;
        this.agent.speed = this.startSpeed;
        this.isEnabled = true;
        MusicManager.instance.EnableMusic(5);
        base.GetComponent<SoundManager>().enabled = true;
    }

    // Token: 0x06000091 RID: 145 RVA: 0x0000399C File Offset: 0x00001B9C
    private void Update()
    {
        if (this.isEnabled)
        {
            this.healthSlider.value = this.health / this.startHealth;
            this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
            this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
            if (!this.playerInSightRange && !this.playerInAttackRange)
            {
                this.Patroling();
            }
            if (this.playerInSightRange && !this.playerInAttackRange)
            {
                this.ChasePlayer();
            }
            if (this.playerInAttackRange && this.playerInSightRange)
            {
                this.AttackPlayer();
            }
        }
    }

    // Token: 0x06000092 RID: 146 RVA: 0x00003A5C File Offset: 0x00001C5C
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x06000093 RID: 147 RVA: 0x00003AC0 File Offset: 0x00001CC0
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x06000094 RID: 148 RVA: 0x00003B66 File Offset: 0x00001D66
    private void ChasePlayer()
    {
        this.agent.SetDestination(this.player.position);
        if (!this.isAttacking)
        {
            this.isAttacking = true;
            this.ChooseMoveset();
        }
    }

    // Token: 0x06000095 RID: 149 RVA: 0x00003B94 File Offset: 0x00001D94
    private void AttackPlayer()
    {
        this.agent.SetDestination(base.transform.position);
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x06000096 RID: 150 RVA: 0x00003C30 File Offset: 0x00001E30
    public void ChooseMoveset()
    {
        if (this.health < 170f && !this.hasActivatedForceField)
        {
            this.ActivateForceField();
            return;
        }
        if (this.health < 120f && !this.hasSummonedPhantoms && !this.forceField.activeInHierarchy)
        {
            this.SummonPhantoms();
            return;
        }
        int num = Random.Range(0, 3);
        if (num == 0)
        {
            base.StartCoroutine(this.SpawnGroupOfBullets(1, 6f));
            return;
        }
        if (num != 1)
        {
            if (num == 2)
            {
                if (this.health < 120f && !this.forceField.activeInHierarchy && this.canSummonPhantoms)
                {
                    this.SummonPhantoms();
                }
                else
                {
                    base.StartCoroutine(this.SpawnCandles());
                }
                if (this.health < 70f)
                {
                    for (int i = 0; i < 7; i++)
                    {
                        base.StartCoroutine(this.SummonLightning());
                    }
                }
            }
            return;
        }
        if (this.health > 170f || !this.canActivateForceField)
        {
            base.StartCoroutine(this.SpawnCandles());
            return;
        }
        this.ActivateForceField();
    }

    // Token: 0x06000097 RID: 151 RVA: 0x00003D30 File Offset: 0x00001F30
    public IEnumerator SpawnGroupOfBullets(int totalbullets, float cooldown)
    {
        int numberOfBullets = 0;
        while (numberOfBullets <= totalbullets)
        {
            this.SpawnBullet(80f);
            int num = numberOfBullets;
            numberOfBullets = num + 1;
            yield return new WaitForSeconds(cooldown);
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x06000098 RID: 152 RVA: 0x00003D4D File Offset: 0x00001F4D
    public IEnumerator SpawnCandles()
    {
        if (this.health > 100f)
        {
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(0.5f);
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(2f);
        }
        else
        {
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(0.5f);
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(0.5f);
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(0.5f);
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x06000099 RID: 153 RVA: 0x00003D5C File Offset: 0x00001F5C
    public void SpawnCandlesPack()
    {
        this.SpawnCandle(new Vector3(-0.1f, 0f), -10f);
        this.SpawnCandle(new Vector3(0f, 0f), 0f);
        this.SpawnCandle(new Vector3(0.1f, 0f), 10f);
        if (this.health < 100f)
        {
            this.SpawnCandle(new Vector3(-0.2f, 0f), -20f);
            this.SpawnCandle(new Vector3(0.2f, 0f), 20f);
        }
    }

    // Token: 0x0600009A RID: 154 RVA: 0x00003DF8 File Offset: 0x00001FF8
    public void SpawnCandle(Vector3 positionOffset, float rotationOffset)
    {
        this.attackPos.LookAt(this.player);
        Quaternion rotation = Quaternion.Euler(this.attackPos.eulerAngles.x + 90f, this.attackPos.eulerAngles.y + rotationOffset, this.attackPos.eulerAngles.z);
        Object.Instantiate<GameObject>(this.candle, this.attackPos.position + positionOffset, rotation);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
    }

    // Token: 0x0600009B RID: 155 RVA: 0x00003E90 File Offset: 0x00002090
    public void SpawnBullet(float rotationOffset)
    {
        this.attackPos.LookAt(this.player);
        this.newBullet = Object.Instantiate<GameObject>(this.bullet, this.attackPos.position, this.attackPos.rotation);
        this.newBullet.transform.parent = this.attackPos;
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
    }

    // Token: 0x0600009C RID: 156 RVA: 0x00003F08 File Offset: 0x00002108
    public void ActivateForceField()
    {
        if (!this.hasSeenTip)
        {
            this.hasSeenTip = true;
            this.tipTextAnimator.SetTrigger("isTipping");
            this.cutsceneChecker.HasSeenTip();
        }
        this.hasActivatedForceField = true;
        this.canActivateForceField = false;
        this.forceField.SetActive(true);
        this.forceField.GetComponent<Animator>().SetBool("isActivated", true);
        this.candleController.LightCandles();
        this.isAttacking = false;
    }

    // Token: 0x0600009D RID: 157 RVA: 0x00003F81 File Offset: 0x00002181
    public IEnumerator DeactivateForceField()
    {
        this.forceField.GetComponent<Animator>().SetBool("isActivated", false);
        yield return new WaitForSeconds(0.5f);
        this.forceField.SetActive(false);
        yield return new WaitForSeconds(15f);
        this.canActivateForceField = true;
        yield break;
    }

    // Token: 0x0600009E RID: 158 RVA: 0x00003F90 File Offset: 0x00002190
    public void SummonPhantoms()
    {
        this.hasSummonedPhantoms = true;
        this.canSummonPhantoms = false;
        this.damage = 0;
        this.agent.speed = 0f;
        Object.Instantiate<GameObject>(this.phantomEffect, base.transform.position, Quaternion.identity);
        foreach (object obj in base.transform)
        {
            ((Transform)obj).gameObject.SetActive(false);
        }
        if (this.health > 70f)
        {
            this.phantomSpawner.enabled = true;
            return;
        }
        this.phantomHardSpawner.enabled = true;
    }

    // Token: 0x0600009F RID: 159 RVA: 0x00004054 File Offset: 0x00002254
    public void FinishPhantoms()
    {
        base.StartCoroutine(this.ReturnBoss());
    }

    // Token: 0x060000A0 RID: 160 RVA: 0x00004063 File Offset: 0x00002263
    public IEnumerator ReturnBoss()
    {
        this.damage = this.startDamage;
        this.agent.speed = this.startSpeed;
        this.canActivateForceField = false;
        foreach (object obj in base.transform)
        {
            ((Transform)obj).gameObject.SetActive(true);
        }
        this.isAttacking = false;
        yield return new WaitForSeconds(20f);
        this.canSummonPhantoms = true;
        this.canActivateForceField = true;
        yield break;
    }

    // Token: 0x060000A1 RID: 161 RVA: 0x00004072 File Offset: 0x00002272
    public IEnumerator SummonLightning()
    {
        Vector3 lightningPos = this.RandomPointInBox(new Vector3(18f, 0f, 18f));
        Object.Instantiate<GameObject>(this.preLightning, lightningPos, Quaternion.identity);
        yield return new WaitForSeconds(1f);
        Object.Instantiate<GameObject>(this.lightning, lightningPos, Quaternion.identity);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
        yield break;
    }

    // Token: 0x060000A2 RID: 162 RVA: 0x00004084 File Offset: 0x00002284
    private Vector3 RandomPointInBox(Vector3 size)
    {
        return new Vector3(this.player.position.x, 0f, this.player.position.z) + new Vector3((Random.value - 0.5f) * size.x, (Random.value - 0.5f) * size.y, (Random.value - 0.5f) * size.z);
    }

    // Token: 0x060000A3 RID: 163 RVA: 0x000040FB File Offset: 0x000022FB
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x060000A4 RID: 164 RVA: 0x00004104 File Offset: 0x00002304
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x060000A5 RID: 165 RVA: 0x00004134 File Offset: 0x00002334
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.finalMushroom, base.transform.position, Quaternion.identity);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x060000A6 RID: 166 RVA: 0x00004190 File Offset: 0x00002390
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x04000071 RID: 113
    public LayerMask whatIsGround;

    // Token: 0x04000072 RID: 114
    public LayerMask whatIsPlayer;

    // Token: 0x04000073 RID: 115
    public Vector3 walkPoint;

    // Token: 0x04000074 RID: 116
    private bool walkPointSet;

    // Token: 0x04000075 RID: 117
    public float walkPointRange;

    // Token: 0x04000076 RID: 118
    public float timeBetweenAttacks;

    // Token: 0x04000077 RID: 119
    private bool isAttacking;

    // Token: 0x04000078 RID: 120
    private bool alreadyAttacked;

    // Token: 0x04000079 RID: 121
    public float sightRange;

    // Token: 0x0400007A RID: 122
    public float attackRange;

    // Token: 0x0400007B RID: 123
    public bool playerInSightRange;

    // Token: 0x0400007C RID: 124
    public bool playerInAttackRange;

    // Token: 0x0400007F RID: 127
    public float startHealth;

    // Token: 0x04000080 RID: 128
    public float health;

    // Token: 0x04000081 RID: 129
    public int startDamage;

    // Token: 0x04000082 RID: 130
    public float startSpeed;

    // Token: 0x04000083 RID: 131
    private int damage;

    // Token: 0x04000084 RID: 132
    private int restCount;

    // Token: 0x04000085 RID: 133
    private bool canActivateForceField = true;

    // Token: 0x04000086 RID: 134
    private bool canSummonPhantoms = true;

    // Token: 0x04000087 RID: 135
    private bool hasActivatedForceField;

    // Token: 0x04000088 RID: 136
    private bool hasSummonedPhantoms;

    // Token: 0x04000089 RID: 137
    private bool isEnabled;

    // Token: 0x0400008A RID: 138
    public GameObject cutscene;

    // Token: 0x0400008B RID: 139
    public GameObject bullet;

    // Token: 0x0400008C RID: 140
    public GameObject candle;

    // Token: 0x0400008D RID: 141
    public GameObject forceField;

    // Token: 0x0400008E RID: 142
    public GameObject preLightning;

    // Token: 0x0400008F RID: 143
    public GameObject lightning;

    // Token: 0x04000090 RID: 144
    public Transform attackPos;

    // Token: 0x04000091 RID: 145
    public Transform lightningCenter;

    // Token: 0x04000092 RID: 146
    public GameObject magicSound;

    // Token: 0x04000093 RID: 147
    public GameObject phantomEffect;

    // Token: 0x04000094 RID: 148
    public GameObject deathEffect;

    // Token: 0x04000095 RID: 149
    public GameObject finalMushroom;

    // Token: 0x04000096 RID: 150
    public CandleController candleController;

    // Token: 0x04000097 RID: 151
    public CurveSpawner phantomSpawner;

    // Token: 0x04000098 RID: 152
    public CurveSpawner phantomHardSpawner;

    // Token: 0x04000099 RID: 153
    public CutsceneChecker cutsceneChecker;

    // Token: 0x0400009A RID: 154
    public Animator tipTextAnimator;

    // Token: 0x0400009B RID: 155
    public Slider healthSlider;

    // Token: 0x0400009C RID: 156
    private NavMeshAgent agent;

    // Token: 0x0400009D RID: 157
    public Transform player;

    // Token: 0x0400009E RID: 158
    public Transform playerLookPoint;

    // Token: 0x0400009F RID: 159
    private Animator animator;

    // Token: 0x040000A0 RID: 160
    private GameObject newBullet;

    // Token: 0x040000A1 RID: 161
    private Vector3 stickDirection;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x02000003 RID: 3
public class BooTrigger : MonoBehaviour
{
    // Token: 0x06000005 RID: 5 RVA: 0x00002138 File Offset: 0x00000338
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.border.SetActive(false);
            this.doorsAnimator.SetTrigger("isOpened");
            this.navMeshAgent.speed = 8f;
            Object.Instantiate<GameObject>(this.sound, other.transform.position, Quaternion.identity).transform.parent = other.transform;
            MusicManager.instance.EnableMusic(1);
            base.GetComponent<Collider>().enabled = false;
        }
    }

    // Token: 0x04000006 RID: 6
    [SerializeField]
    private Animator doorsAnimator;

    // Token: 0x04000007 RID: 7
    [SerializeField]
    private NavMeshAgent navMeshAgent;

    // Token: 0x04000008 RID: 8
    [SerializeField]
    private GameObject sound;

    // Token: 0x04000009 RID: 9
    [SerializeField]
    private GameObject border;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Token: 0x02000022 RID: 34
public class Boss : MonoBehaviour
{
    // Token: 0x060000A8 RID: 168 RVA: 0x000041F4 File Offset: 0x000023F4
    private void Awake()
    {
        this.player = GameObject.FindGameObjectWithTag("Player").transform;
        this.agent = base.GetComponent<NavMeshAgent>();
        this.health = this.startHealth;
        this.healthSlider = Object.FindObjectOfType<MushroomGame>().bossHP.GetComponent<Slider>();
    }

    // Token: 0x060000A9 RID: 169 RVA: 0x00004243 File Offset: 0x00002443
    private void Start()
    {
        this.healthSlider.gameObject.SetActive(true);
    }

    // Token: 0x060000AA RID: 170 RVA: 0x00004258 File Offset: 0x00002458
    private void Update()
    {
        this.healthSlider.value = this.health / this.startHealth;
        this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
        if (!this.playerInSightRange && !this.playerInAttackRange)
        {
            this.Patroling();
        }
        if (this.playerInSightRange && !this.playerInAttackRange)
        {
            this.ChasePlayer();
        }
        if (this.playerInAttackRange && this.playerInSightRange)
        {
            this.AttackPlayer();
        }
    }

    // Token: 0x060000AB RID: 171 RVA: 0x00004310 File Offset: 0x00002510
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x060000AC RID: 172 RVA: 0x00004374 File Offset: 0x00002574
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x060000AD RID: 173 RVA: 0x0000441A File Offset: 0x0000261A
    private void ChasePlayer()
    {
        this.agent.SetDestination(this.player.position);
        if (!this.isAttacking)
        {
            this.isAttacking = true;
            this.ChooseMoveset();
        }
    }

    // Token: 0x060000AE RID: 174 RVA: 0x00004448 File Offset: 0x00002648
    private void AttackPlayer()
    {
        this.agent.SetDestination(base.transform.position);
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x060000AF RID: 175 RVA: 0x000044E4 File Offset: 0x000026E4
    public void ChooseMoveset()
    {
        int num = Random.Range(0, 3);
        if (num == 0)
        {
            base.StartCoroutine(this.SpawnGroupSticks(false, 5, 0.3f));
            return;
        }
        if (num != 1)
        {
            if (num == 2)
            {
                for (int i = 0; i < 7; i++)
                {
                    base.StartCoroutine(this.SummonLightning());
                }
            }
            return;
        }
        if (this.health > 15f)
        {
            base.StartCoroutine(this.SpawnGroupSticks(true, 3, 0.6f));
            return;
        }
        base.StartCoroutine(this.InstantiateEarthquake());
    }

    // Token: 0x060000B0 RID: 176 RVA: 0x00004562 File Offset: 0x00002762
    public IEnumerator SpawnGroupSticks(bool isNormal, int totalSticks, float cooldown)
    {
        int numberOfSticks = 0;
        while (numberOfSticks <= totalSticks)
        {
            this.SpawnStick(isNormal);
            int num = numberOfSticks;
            numberOfSticks = num + 1;
            yield return new WaitForSeconds(cooldown);
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x060000B1 RID: 177 RVA: 0x00004588 File Offset: 0x00002788
    public void SpawnStick(bool isNormal)
    {
        if (isNormal)
        {
            Object.Instantiate<GameObject>(this.stick, this.attackPosNormal.position, Quaternion.Euler(0f, (float)Random.Range(0, 360), (float)Random.Range(0, 360)));
        }
        else
        {
            Object.Instantiate<GameObject>(this.stick, this.attackPosDown.position, this.attackPosDown.rotation);
        }
        Object.Instantiate<GameObject>(this.magicSound, this.attackPosNormal.position, Quaternion.identity);
    }

    // Token: 0x060000B2 RID: 178 RVA: 0x00004611 File Offset: 0x00002811
    public IEnumerator SummonLightning()
    {
        Vector3 lightningPos = this.RandomPointInBox(new Vector3(15f, 0f, 15f));
        Object.Instantiate<GameObject>(this.preLightning, lightningPos, Quaternion.identity);
        yield return new WaitForSeconds(1f);
        Object.Instantiate<GameObject>(this.lightning, lightningPos, Quaternion.identity);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPosNormal.position, Quaternion.identity);
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x060000B3 RID: 179 RVA: 0x00004620 File Offset: 0x00002820
    public IEnumerator InstantiateEarthquake()
    {
        Object.Instantiate<GameObject>(this.earthquake, this.lightningCenter.position, this.lightningCenter.rotation);
        yield return new WaitForSeconds(3f);
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x060000B4 RID: 180 RVA: 0x00004630 File Offset: 0x00002830
    private Vector3 RandomPointInBox(Vector3 size)
    {
        return new Vector3(this.player.position.x, 0f, this.player.position.z) + new Vector3((Random.value - 0.5f) * size.x, (Random.value - 0.5f) * size.y, (Random.value - 0.5f) * size.z);
    }

    // Token: 0x060000B5 RID: 181 RVA: 0x000046A7 File Offset: 0x000028A7
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x060000B6 RID: 182 RVA: 0x000046B0 File Offset: 0x000028B0
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x060000B7 RID: 183 RVA: 0x000046E0 File Offset: 0x000028E0
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.finalMushroom, base.transform.position, Quaternion.identity);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x060000B8 RID: 184 RVA: 0x0000473C File Offset: 0x0000293C
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x040000A2 RID: 162
    public LayerMask whatIsGround;

    // Token: 0x040000A3 RID: 163
    public LayerMask whatIsPlayer;

    // Token: 0x040000A4 RID: 164
    public Vector3 walkPoint;

    // Token: 0x040000A5 RID: 165
    private bool walkPointSet;

    // Token: 0x040000A6 RID: 166
    public float walkPointRange;

    // Token: 0x040000A7 RID: 167
    public float timeBetweenAttacks;

    // Token: 0x040000A8 RID: 168
    private bool isAttacking;

    // Token: 0x040000A9 RID: 169
    private bool alreadyAttacked;

    // Token: 0x040000AA RID: 170
    public float sightRange;

    // Token: 0x040000AB RID: 171
    public float attackRange;

    // Token: 0x040000AC RID: 172
    public bool playerInSightRange;

    // Token: 0x040000AD RID: 173
    public bool playerInAttackRange;

    // Token: 0x040000AE RID: 174
    public float startHealth;

    // Token: 0x040000AF RID: 175
    private float health;

    // Token: 0x040000B0 RID: 176
    public int damage;

    // Token: 0x040000B1 RID: 177
    public GameObject stick;

    // Token: 0x040000B2 RID: 178
    public GameObject preLightning;

    // Token: 0x040000B3 RID: 179
    public GameObject lightning;

    // Token: 0x040000B4 RID: 180
    public GameObject earthquake;

    // Token: 0x040000B5 RID: 181
    public Transform attackPosNormal;

    // Token: 0x040000B6 RID: 182
    public Transform attackPosDown;

    // Token: 0x040000B7 RID: 183
    public Transform lightningCenter;

    // Token: 0x040000B8 RID: 184
    public GameObject magicSound;

    // Token: 0x040000B9 RID: 185
    public GameObject deathEffect;

    // Token: 0x040000BA RID: 186
    public GameObject finalMushroom;

    // Token: 0x040000BB RID: 187
    public Slider healthSlider;

    // Token: 0x040000BC RID: 188
    private NavMeshAgent agent;

    // Token: 0x040000BD RID: 189
    private Transform player;

    // Token: 0x040000BE RID: 190
    private GameObject newStick;

    // Token: 0x040000BF RID: 191
    private Vector3 stickDirection;
}

using System;
using UnityEngine;

// Token: 0x02000023 RID: 35
public class BossMovesets : MonoBehaviour
{
    // Token: 0x060000BA RID: 186 RVA: 0x00004791 File Offset: 0x00002991
    private void Start()
    {
    }

    // Token: 0x060000BB RID: 187 RVA: 0x00004793 File Offset: 0x00002993
    private void Update()
    {
    }
}

using System;
using UnityEngine;

// Token: 0x0200001C RID: 28
public class BossMusicSwitcher : MonoBehaviour
{
    // Token: 0x06000077 RID: 119 RVA: 0x00003552 File Offset: 0x00001752
    private void Start()
    {
        this.music = Object.FindObjectOfType<MusicManager>();
    }

    // Token: 0x06000078 RID: 120 RVA: 0x00003560 File Offset: 0x00001760
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (this.music.audioSource.clip != this.music.musicVariants[5] || this.music.audioSource.clip != this.music.musicVariants[6])
            {
                if (Random.Range(0, 10) == 9)
                {
                    this.music.EnableMusic(6);
                    return;
                }
                this.music.EnableMusic(5);
                return;
            }
            else
            {
                this.music.EnableMusic(0);
            }
        }
    }

    // Token: 0x04000063 RID: 99
    private MusicManager music;
}

using System;
using UnityEngine;

// Token: 0x02000004 RID: 4
public class BossTeleport : MonoBehaviour
{
    // Token: 0x06000007 RID: 7 RVA: 0x000021C8 File Offset: 0x000003C8
    private void OnEnable()
    {
        ItemCollector itemCollector = this.itemCollector;
        itemCollector.hasCollected = (Action)Delegate.Combine(itemCollector.hasCollected, new Action(this.CheckFlags));
    }

    // Token: 0x06000008 RID: 8 RVA: 0x000021F1 File Offset: 0x000003F1
    private void OnDisable()
    {
        ItemCollector itemCollector = this.itemCollector;
        itemCollector.hasCollected = (Action)Delegate.Remove(itemCollector.hasCollected, new Action(this.CheckFlags));
    }

    // Token: 0x06000009 RID: 9 RVA: 0x0000221A File Offset: 0x0000041A
    private void Start()
    {
        this.CheckFlags();
    }

    // Token: 0x0600000A RID: 10 RVA: 0x00002222 File Offset: 0x00000422
    private void CheckFlags()
    {
        if (this.itemCollector.items >= this.flagsCount)
        {
            this.EnablePortal();
        }
    }

    // Token: 0x0600000B RID: 11 RVA: 0x0000223D File Offset: 0x0000043D
    private void EnablePortal()
    {
        base.GetComponent<BoxCollider>().enabled = true;
        base.transform.GetChild(0).gameObject.SetActive(true);
    }

    // Token: 0x0600000C RID: 12 RVA: 0x00002264 File Offset: 0x00000464
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.root.position = this.teleportPoint.position;
            GravityController.instance.SetEarthGravity();
            base.Invoke("ActivateBoss", 0.5f);
        }
    }

    // Token: 0x0600000D RID: 13 RVA: 0x000022B3 File Offset: 0x000004B3
    public void ActivateBoss()
    {
        Debug.Log("ACTIVATIN");
        this.boss.SetActive(true);
    }

    // Token: 0x0400000A RID: 10
    [SerializeField]
    private GameObject boss;

    // Token: 0x0400000B RID: 11
    [SerializeField]
    private Transform teleportPoint;

    // Token: 0x0400000C RID: 12
    [SerializeField]
    private ItemCollector itemCollector;

    // Token: 0x0400000D RID: 13
    [SerializeField]
    private int flagsCount;
}

using System;
using UnityEngine;

// Token: 0x0200004F RID: 79
public class BuoyancyObject : MonoBehaviour
{
    // Token: 0x060001A0 RID: 416 RVA: 0x000081E0 File Offset: 0x000063E0
    private void FixedUpdate()
    {
        this.FloatersUnderWater = 0;
        for (int i = 0; i < this.Floaters.Length; i++)
        {
            float num = this.Floaters[i].position.y - this.WaterHeight;
            if (num < 0f)
            {
                this.Rb.AddForceAtPosition(Vector3.up * this.FloatingPower * Mathf.Abs(num), this.Floaters[i].position, ForceMode.Force);
                this.FloatersUnderWater++;
                if (!this.Underwater)
                {
                    this.Underwater = true;
                    this.SwitchState(true);
                }
            }
        }
        if (this.Underwater && this.FloatersUnderWater == 0)
        {
            this.Underwater = false;
            this.SwitchState(false);
        }
    }

    // Token: 0x060001A1 RID: 417 RVA: 0x000082A4 File Offset: 0x000064A4
    private void SwitchState(bool isUnderwater)
    {
        if (isUnderwater)
        {
            this.Rb.drag = this.UnderWaterDrag;
            this.Rb.angularDrag = this.UnderWaterAngularDrag;
            return;
        }
        this.Rb.drag = this.AirDrag;
        this.Rb.angularDrag = this.AirAngularDrag;
    }

    // Token: 0x040001FA RID: 506
    public Transform[] Floaters;

    // Token: 0x040001FB RID: 507
    public float UnderWaterDrag = 3f;

    // Token: 0x040001FC RID: 508
    public float UnderWaterAngularDrag = 1f;

    // Token: 0x040001FD RID: 509
    public float AirDrag;

    // Token: 0x040001FE RID: 510
    public float AirAngularDrag = 0.05f;

    // Token: 0x040001FF RID: 511
    public float FloatingPower = 15f;

    // Token: 0x04000200 RID: 512
    public float WaterHeight;

    // Token: 0x04000201 RID: 513
    public Rigidbody Rb;

    // Token: 0x04000202 RID: 514
    private bool Underwater;

    // Token: 0x04000203 RID: 515
    private int FloatersUnderWater;
}

using System;
using System.Collections;
using UnityEngine;

// Token: 0x0200003A RID: 58
public class BurgerBehaviour : MonoBehaviour
{
    // Token: 0x0600013D RID: 317 RVA: 0x000068C3 File Offset: 0x00004AC3
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.burgerUsual.SetActive(false);
            this.burgerUnusual.SetActive(true);
        }
    }

    // Token: 0x0600013E RID: 318 RVA: 0x000068EA File Offset: 0x00004AEA
    public IEnumerator OnBurgerDestroyed()
    {
        this.spawner.enabled = true;
        yield return new WaitForSeconds(15.5f);
        this.spawner.enabled = false;
        this.lockUI.OnFoodKilled();
        yield break;
    }

    // Token: 0x04000180 RID: 384
    public GameObject burgerUsual;

    // Token: 0x04000181 RID: 385
    public GameObject burgerUnusual;

    // Token: 0x04000182 RID: 386
    public CurveSpawner spawner;

    // Token: 0x04000183 RID: 387
    public LockUI lockUI;
}

using System;
using UnityEngine;

// Token: 0x02000005 RID: 5
public class Candle : MonoBehaviour
{
    // Token: 0x0600000F RID: 15 RVA: 0x000022D4 File Offset: 0x000004D4
    public void BlowOutCandle()
    {
        if (this.isLit)
        {
            this.isLit = false;
            Object.Instantiate<GameObject>(this.bang, this.bangPoint.position, Quaternion.identity);
            Action action = this.isBlown;
            if (action != null)
            {
                action();
            }
            this.LightCandle(false);
        }
    }

    // Token: 0x06000010 RID: 16 RVA: 0x00002324 File Offset: 0x00000524
    public void LightCandle(bool state)
    {
        base.transform.GetChild(0).gameObject.SetActive(state);
        this.isLit = state;
    }

    // Token: 0x0400000E RID: 14
    public bool isLit;

    // Token: 0x0400000F RID: 15
    public Action isBlown;

    // Token: 0x04000010 RID: 16
    [SerializeField]
    private Transform bangPoint;

    // Token: 0x04000011 RID: 17
    [SerializeField]
    private GameObject bang;
}

using System;
using UnityEngine;

// Token: 0x02000045 RID: 69
public class CandleBullet : MonoBehaviour
{
    // Token: 0x0600016C RID: 364 RVA: 0x00007278 File Offset: 0x00005478
    private void Update()
    {
        base.transform.Translate(Vector3.up * this.speed * Time.deltaTime);
    }

    // Token: 0x040001B2 RID: 434
    [SerializeField]
    private float speed;
}

using System;
using UnityEngine;

// Token: 0x02000006 RID: 6
public class CandleController : MonoBehaviour
{
    // Token: 0x06000012 RID: 18 RVA: 0x0000234C File Offset: 0x0000054C
    private void OnEnable()
    {
        foreach (Candle candle in this.candles)
        {
            candle.isBlown = (Action)Delegate.Combine(candle.isBlown, new Action(this.CheckCandles));
        }
    }

    // Token: 0x06000013 RID: 19 RVA: 0x00002394 File Offset: 0x00000594
    private void OnDisable()
    {
        foreach (Candle candle in this.candles)
        {
            candle.isBlown = (Action)Delegate.Remove(candle.isBlown, new Action(this.CheckCandles));
        }
    }

    // Token: 0x06000014 RID: 20 RVA: 0x000023DC File Offset: 0x000005DC
    public void LightCandles()
    {
        Candle[] array = this.candles;
        for (int i = 0; i < array.Length; i++)
        {
            array[i].LightCandle(true);
        }
    }

    // Token: 0x06000015 RID: 21 RVA: 0x00002408 File Offset: 0x00000608
    public void CheckCandles()
    {
        this.boss.TakeDamage(2);
        Candle[] array = this.candles;
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i].isLit)
            {
                return;
            }
        }
        base.StartCoroutine(this.boss.DeactivateForceField());
    }

    // Token: 0x04000012 RID: 18
    [SerializeField]
    private Candle[] candles;

    // Token: 0x04000013 RID: 19
    [SerializeField]
    private BooBoss boss;
}

using System;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x0200004C RID: 76
public class Card : MonoBehaviour
{
    // Token: 0x06000186 RID: 390 RVA: 0x000079E6 File Offset: 0x00005BE6
    private void Start()
    {
        this.animator = base.GetComponent<Animator>();
        this.cardImage = base.GetComponent<Image>();
        this.cardManager = Object.FindObjectOfType<CardManager>();
        this.cardImage.sprite = this.backSprite;
    }

    // Token: 0x06000187 RID: 391 RVA: 0x00007A1C File Offset: 0x00005C1C
    public void OnClick()
    {
        if (!this.cardManager.CanFlipCard(this))
        {
            return;
        }
        this.animator.SetBool("isFaceUp", true);
        this.animator.SetTrigger("isTriggered");
        this.cardManager.CardRevealed(this);
    }

    // Token: 0x06000188 RID: 392 RVA: 0x00007A5A File Offset: 0x00005C5A
    public void RevealFace()
    {
        this.cardImage.sprite = this.faceSprite;
    }

    // Token: 0x06000189 RID: 393 RVA: 0x00007A6D File Offset: 0x00005C6D
    public void Hide()
    {
        this.animator.SetBool("isFaceUp", false);
        this.animator.SetTrigger("isTriggered");
    }

    // Token: 0x0600018A RID: 394 RVA: 0x00007A90 File Offset: 0x00005C90
    public void RevealBack()
    {
        this.cardImage.sprite = this.backSprite;
    }

    // Token: 0x0600018B RID: 395 RVA: 0x00007AA3 File Offset: 0x00005CA3
    public void BooAnimation()
    {
        this.animator.SetTrigger("screamer");
    }

    // Token: 0x040001D7 RID: 471
    private Animator animator;

    // Token: 0x040001D8 RID: 472
    public Sprite faceSprite;

    // Token: 0x040001D9 RID: 473
    public Sprite backSprite;

    // Token: 0x040001DA RID: 474
    private Image cardImage;

    // Token: 0x040001DB RID: 475
    private CardManager cardManager;
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x0200004D RID: 77
public class CardManager : MonoBehaviour
{
    // Token: 0x0600018D RID: 397 RVA: 0x00007ABD File Offset: 0x00005CBD
    private void Start()
    {
    }

    // Token: 0x0600018E RID: 398 RVA: 0x00007ABF File Offset: 0x00005CBF
    private void OnEnable()
    {
        this.DeactivatePlayer();
    }

    // Token: 0x0600018F RID: 399 RVA: 0x00007AC7 File Offset: 0x00005CC7
    private void OnDisable()
    {
        this.ActivatePlayer();
    }

    // Token: 0x06000190 RID: 400 RVA: 0x00007AD0 File Offset: 0x00005CD0
    public void StartGame(List<Sprite> cardFaces)
    {
        Vector2 vector = new Vector2((float)(-(float)this.gridCols) / 2f, (float)this.gridRows / 2f);
        List<Sprite> list = new List<Sprite>(cardFaces);
        list.AddRange(cardFaces);
        this.Shuffle(list);
        for (int i = 0; i < this.gridRows; i++)
        {
            for (int j = 0; j < this.gridCols; j++)
            {
                GameObject gameObject = Object.Instantiate<GameObject>(this.cardPrefab, this.cardParent);
                Card component = gameObject.GetComponent<Card>();
                int index = i * this.gridCols + j;
                component.faceSprite = list[index];
                RectTransform component2 = gameObject.GetComponent<RectTransform>();
                float x = vector.x + (float)j * (this.cellWidth + this.offsetX);
                float y = vector.y - (float)i * (this.cellHeight + this.offsetY);
                component2.anchoredPosition = new Vector2(x, y);
            }
        }
    }

    // Token: 0x06000191 RID: 401 RVA: 0x00007BB4 File Offset: 0x00005DB4
    public void CardRevealed(Card card)
    {
        if (this.firstRevealed == null)
        {
            this.firstRevealed = card;
            if (card.faceSprite == this.booSprite && !this._hasSeenBoo)
            {
                this._hasSeenBoo = true;
                Object.Instantiate<GameObject>(this.screamerSound, this.player.transform.position, Quaternion.identity);
                Object.Instantiate<GameObject>(this.booSound, this.player.transform.position, Quaternion.identity);
                card.BooAnimation();
                return;
            }
        }
        else
        {
            this.secondRevealed = card;
            if (card.faceSprite == this.booSprite && !this._hasSeenBoo)
            {
                this._hasSeenBoo = true;
                Object.Instantiate<GameObject>(this.screamerSound, this.player.transform.position, Quaternion.identity);
                Object.Instantiate<GameObject>(this.booSound, this.player.transform.position, Quaternion.identity);
                card.BooAnimation();
            }
            base.StartCoroutine(this.CheckMatch());
        }
    }

    // Token: 0x06000192 RID: 402 RVA: 0x00007CC5 File Offset: 0x00005EC5
    private IEnumerator CheckMatch()
    {
        if (this.firstRevealed.faceSprite == this.secondRevealed.faceSprite)
        {
            yield return new WaitForSeconds(0.5f);
            this.MakeCardInvisible(this.firstRevealed);
            this.MakeCardInvisible(this.secondRevealed);
        }
        else
        {
            yield return new WaitForSeconds(1f);
            this.firstRevealed.Hide();
            this.secondRevealed.Hide();
        }
        this.firstRevealed = null;
        this.secondRevealed = null;
        this.CheckLevelComplete();
        yield break;
    }

    // Token: 0x06000193 RID: 403 RVA: 0x00007CD4 File Offset: 0x00005ED4
    private void MakeCardInvisible(Card card)
    {
        Image component = card.GetComponent<Image>();
        if (component != null)
        {
            component.color = new Color(1f, 1f, 1f, 0f);
        }
        card.GetComponent<Button>().interactable = false;
    }

    // Token: 0x06000194 RID: 404 RVA: 0x00007D1C File Offset: 0x00005F1C
    private void CheckLevelComplete()
    {
        using (IEnumerator enumerator = this.cardParent.GetEnumerator())
        {
            while (enumerator.MoveNext())
            {
                if (((Transform)enumerator.Current).GetComponent<Image>().color.a > 0f)
                {
                    return;
                }
            }
        }
        if (this.levelToLoad < 3)
        {
            this.levelToLoad++;
            this.LoadLevel(this.levelToLoad);
        }
        else
        {
            this.memoryGameTrigger.OnGameOver();
        }
        Debug.Log("Level Complete!");
    }

    // Token: 0x06000195 RID: 405 RVA: 0x00007DC0 File Offset: 0x00005FC0
    public bool CanFlipCard(Card card)
    {
        return card != this.firstRevealed && this.secondRevealed == null;
    }

    // Token: 0x06000196 RID: 406 RVA: 0x00007DE0 File Offset: 0x00005FE0
    private void Shuffle(List<Sprite> deck)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            Sprite value = deck[i];
            int index = Random.Range(i, deck.Count);
            deck[i] = deck[index];
            deck[index] = value;
        }
    }

    // Token: 0x06000197 RID: 407 RVA: 0x00007E2C File Offset: 0x0000602C
    private void ClearPreviousCards()
    {
        foreach (object obj in this.cardParent)
        {
            Object.Destroy(((Transform)obj).gameObject);
        }
    }

    // Token: 0x06000198 RID: 408 RVA: 0x00007E88 File Offset: 0x00006088
    public void LoadLevel(int level)
    {
        this.ClearPreviousCards();
        switch (level)
        {
            case 1:
                this.gridRows = 2;
                this.gridCols = 4;
                this.gridLayoutManager.ConfigureGrid(this.gridRows, this.gridCols, 150f, 150f, 10f, 10f);
                this.StartGame(this.cardFacesLevel1);
                return;
            case 2:
                this.gridRows = 3;
                this.gridCols = 4;
                this.gridLayoutManager.ConfigureGrid(this.gridRows, this.gridCols, 150f, 150f, 10f, 10f);
                this.StartGame(this.cardFacesLevel2);
                return;
            case 3:
                this.gridRows = 4;
                this.gridCols = 4;
                this.gridLayoutManager.ConfigureGrid(this.gridRows, this.gridCols, 150f, 150f, 10f, 10f);
                this.StartGame(this.cardFacesLevel3);
                return;
            default:
                return;
        }
    }

    // Token: 0x06000199 RID: 409 RVA: 0x00007F81 File Offset: 0x00006181
    public void Restart()
    {
        this.LoadLevel(this.levelToLoad);
    }

    // Token: 0x0600019A RID: 410 RVA: 0x00007F8F File Offset: 0x0000618F
    public void ActivatePlayer()
    {
        this.gun.SetActive(true);
        this.player.SetupDialogue(false);
        base.gameObject.SetActive(false);
    }

    // Token: 0x0600019B RID: 411 RVA: 0x00007FB5 File Offset: 0x000061B5
    public void DeactivatePlayer()
    {
        this.gun.SetActive(false);
        this.player.SetupDialogue(true);
        this.LoadLevel(this.levelToLoad);
    }

    // Token: 0x040001DC RID: 476
    public List<Sprite> cardFacesLevel1;

    // Token: 0x040001DD RID: 477
    public List<Sprite> cardFacesLevel2;

    // Token: 0x040001DE RID: 478
    public List<Sprite> cardFacesLevel3;

    // Token: 0x040001DF RID: 479
    public GameObject cardPrefab;

    // Token: 0x040001E0 RID: 480
    public Transform cardParent;

    // Token: 0x040001E1 RID: 481
    public GameObject gun;

    // Token: 0x040001E2 RID: 482
    public Sprite booSprite;

    // Token: 0x040001E3 RID: 483
    public PlayerController player;

    // Token: 0x040001E4 RID: 484
    public GridLayoutManager gridLayoutManager;

    // Token: 0x040001E5 RID: 485
    public MemoryGameTrigger memoryGameTrigger;

    // Token: 0x040001E6 RID: 486
    public GameObject screamerSound;

    // Token: 0x040001E7 RID: 487
    public GameObject booSound;

    // Token: 0x040001E8 RID: 488
    private Card firstRevealed;

    // Token: 0x040001E9 RID: 489
    private Card secondRevealed;

    // Token: 0x040001EA RID: 490
    public float cellWidth = 150f;

    // Token: 0x040001EB RID: 491
    public float cellHeight = 150f;

    // Token: 0x040001EC RID: 492
    public float offsetX = 10f;

    // Token: 0x040001ED RID: 493
    public float offsetY = 10f;

    // Token: 0x040001EE RID: 494
    public int gridRows;

    // Token: 0x040001EF RID: 495
    public int gridCols;

    // Token: 0x040001F0 RID: 496
    private int levelToLoad = 1;

    // Token: 0x040001F1 RID: 497
    private bool _hasSeenBoo;
}

using System;
using UnityEngine;

// Token: 0x02000059 RID: 89
public class CaveSoundDetector : MonoBehaviour
{
    // Token: 0x060001CC RID: 460 RVA: 0x00008DAE File Offset: 0x00006FAE
    private void Start()
    {
        this.music = Object.FindObjectOfType<MusicManager>();
    }

    // Token: 0x060001CD RID: 461 RVA: 0x00008DBB File Offset: 0x00006FBB
    private void OnTriggerEnter(Collider other)
    {
        other.CompareTag("Player");
    }

    // Token: 0x0400023E RID: 574
    private MusicManager music;
}

using System;
using UnityEngine;

// Token: 0x02000007 RID: 7
public class ChaseOver : MonoBehaviour
{
    // Token: 0x06000017 RID: 23 RVA: 0x0000245C File Offset: 0x0000065C
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.enemy.TakeDamage(10000);
            this.border.SetActive(true);
            this.player.moveSpeed = 3.5f;
            MusicManager.instance.EnableMusic(0);
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x04000014 RID: 20
    [SerializeField]
    private Enemy enemy;

    // Token: 0x04000015 RID: 21
    [SerializeField]
    private PlayerController player;

    // Token: 0x04000016 RID: 22
    [SerializeField]
    private GameObject border;
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Token: 0x02000070 RID: 112
public class ChatController : MonoBehaviour
{
    // Token: 0x0600022F RID: 559 RVA: 0x0000A87A File Offset: 0x00008A7A
    private void OnEnable()
    {
        this.TMP_ChatInput.onSubmit.AddListener(new UnityAction<string>(this.AddToChatOutput));
    }

    // Token: 0x06000230 RID: 560 RVA: 0x0000A898 File Offset: 0x00008A98
    private void OnDisable()
    {
        this.TMP_ChatInput.onSubmit.RemoveListener(new UnityAction<string>(this.AddToChatOutput));
    }

    // Token: 0x06000231 RID: 561 RVA: 0x0000A8B8 File Offset: 0x00008AB8
    private void AddToChatOutput(string newText)
    {
        this.TMP_ChatInput.text = string.Empty;
        DateTime now = DateTime.Now;
        TMP_Text tmp_ChatOutput = this.TMP_ChatOutput;
        tmp_ChatOutput.text = string.Concat(new string[]
        {
            tmp_ChatOutput.text,
            "[<#FFFF80>",
            now.Hour.ToString("d2"),
            ":",
            now.Minute.ToString("d2"),
            ":",
            now.Second.ToString("d2"),
            "</color>] ",
            newText,
            "\n"
        });
        this.TMP_ChatInput.ActivateInputField();
        this.ChatScrollbar.value = 0f;
    }

    // Token: 0x040002A2 RID: 674
    public TMP_InputField TMP_ChatInput;

    // Token: 0x040002A3 RID: 675
    public TMP_Text TMP_ChatOutput;

    // Token: 0x040002A4 RID: 676
    public Scrollbar ChatScrollbar;
}

using System;
using UnityEngine;

// Token: 0x0200001D RID: 29
public class CheckPoint : MonoBehaviour
{
    // Token: 0x0600007A RID: 122 RVA: 0x000035FC File Offset: 0x000017FC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !this.alreadySaved)
        {
            this.alreadySaved = true;
            this.collector = other.transform.root.GetComponent<ItemCollector>();
            this.SaveData();
        }
    }

    // Token: 0x0600007B RID: 123 RVA: 0x00003636 File Offset: 0x00001836
    private void SaveData()
    {
        this.collector.SaveMushrooms();
        this.collector.GetComponent<PlayerController>().SavePosition();
        Object.FindObjectOfType<MenuExit>(true).SaveItemPresence();
    }

    // Token: 0x04000064 RID: 100
    public Transform point;

    // Token: 0x04000065 RID: 101
    private ItemCollector collector;

    // Token: 0x04000066 RID: 102
    private bool alreadySaved;
}

using System;
using UnityEngine;

// Token: 0x02000008 RID: 8
public static class CursorController
{
    // Token: 0x06000019 RID: 25 RVA: 0x000024C0 File Offset: 0x000006C0
    public static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Token: 0x0600001A RID: 26 RVA: 0x000024CE File Offset: 0x000006CE
    public static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

using System;
using UnityEngine;

// Token: 0x02000046 RID: 70
public class CurveSpawnedObject : MonoBehaviour
{
    // Token: 0x0600016E RID: 366 RVA: 0x000072A7 File Offset: 0x000054A7
    private void OnDestroy()
    {
        this.curveSpawner.OnSpawnedObjectDestroy(this);
    }

    // Token: 0x040001B3 RID: 435
    [HideInInspector]
    public CurveSpawner curveSpawner;
}

using System;
using System.Collections.Generic;
using UnityEngine;

// Token: 0x0200003B RID: 59
public class CurveSpawner : MonoBehaviour
{
    // Token: 0x1700000F RID: 15
    // (get) Token: 0x06000140 RID: 320 RVA: 0x00006901 File Offset: 0x00004B01
    private float startSpawnTime
    {
        get
        {
            return this.decreaseCurve.Evaluate(this._time);
        }
    }

    // Token: 0x06000141 RID: 321 RVA: 0x00006914 File Offset: 0x00004B14
    private void Start()
    {
        this.spawnTime = this.startSpawnTime;
    }

    // Token: 0x06000142 RID: 322 RVA: 0x00006924 File Offset: 0x00004B24
    private void Update()
    {
        if (this.isSpawning)
        {
            this.showTime = this.startSpawnTime;
            this.spawnTime -= Time.deltaTime;
            this._time += Time.deltaTime;
            if (this.spawnTime <= 0f)
            {
                int num = Random.Range(0, this.spawnPoints.Length);
                int num2 = Random.Range(0, this.objectsToSpawn.Length);
                CurveSpawnedObject component = Object.Instantiate<GameObject>(this.objectsToSpawn[num2], this.spawnPoints[num].position, this.spawnPoints[num].rotation).GetComponent<CurveSpawnedObject>();
                component.curveSpawner = this;
                this.spawnedObjects.Add(component);
                this.spawnTime = this.startSpawnTime;
            }
            if (this._time >= this.totalTime)
            {
                this.isSpawning = false;
            }
        }
    }

    // Token: 0x06000143 RID: 323 RVA: 0x000069F8 File Offset: 0x00004BF8
    public void OnSpawnedObjectDestroy(CurveSpawnedObject destroyedObject)
    {
        this.spawnedObjects.Remove(destroyedObject);
        if (this.spawnedObjects.Count == 0 && !this.isSpawning)
        {
            Action action = this.hasDoneSpawning;
            if (action != null)
            {
                action();
            }
            base.enabled = false;
        }
    }

    // Token: 0x06000144 RID: 324 RVA: 0x00006A34 File Offset: 0x00004C34
    public void OnEnable()
    {
        this._time = 0f;
        this.spawnTime = this.startSpawnTime;
        this.isSpawning = true;
    }

    // Token: 0x04000184 RID: 388
    public Transform[] spawnPoints;

    // Token: 0x04000185 RID: 389
    public GameObject[] objectsToSpawn;

    // Token: 0x04000186 RID: 390
    public AnimationCurve decreaseCurve;

    // Token: 0x04000187 RID: 391
    public Action hasDoneSpawning;

    // Token: 0x04000188 RID: 392
    private float _time;

    // Token: 0x04000189 RID: 393
    public float showTime;

    // Token: 0x0400018A RID: 394
    public float totalTime;

    // Token: 0x0400018B RID: 395
    public List<CurveSpawnedObject> spawnedObjects;

    // Token: 0x0400018C RID: 396
    private float spawnTime;

    // Token: 0x0400018D RID: 397
    private bool isSpawning;
}

using System;
using UnityEngine;

// Token: 0x02000067 RID: 103
public class CustomBullet : MonoBehaviour
{
    // Token: 0x0600020C RID: 524 RVA: 0x0000A083 File Offset: 0x00008283
    private void Start()
    {
        this.Setup();
    }

    // Token: 0x0600020D RID: 525 RVA: 0x0000A08B File Offset: 0x0000828B
    private void Update()
    {
        if (this.collisions > this.maxCollisions)
        {
            this.Explode();
        }
        this.maxLifetime -= Time.deltaTime;
        if (this.maxLifetime <= 0f)
        {
            this.Explode();
        }
    }

    // Token: 0x0600020E RID: 526 RVA: 0x0000A0C8 File Offset: 0x000082C8
    private void Explode()
    {
        if (this.explosion != null)
        {
            Object.Instantiate<GameObject>(this.explosion, base.transform.position, Quaternion.identity);
        }
        Collider[] array = Physics.OverlapSphere(base.transform.position, this.explosionRange, this.whatIsEnemies);
        for (int i = 0; i < array.Length; i++)
        {
            Enemy enemy;
            FoxShootable foxShootable;
            Melon melon;
            BooBoss booBoss;
            Candle candle;
            if (array[i].transform.root.TryGetComponent<Enemy>(out enemy))
            {
                enemy.TakeDamage(this.explosionDamage);
            }
            else if (array[i].TryGetComponent<FoxShootable>(out foxShootable))
            {
                foxShootable.TakeDamage(this.explosionDamage);
            }
            else if (array[i].TryGetComponent<Melon>(out melon))
            {
                melon.TakeDamage(this.explosionDamage);
            }
            else if (array[i].transform.root.TryGetComponent<BooBoss>(out booBoss))
            {
                if (!booBoss.forceField.activeInHierarchy)
                {
                    booBoss.TakeDamage(this.explosionDamage);
                }
            }
            else if (array[i].TryGetComponent<Candle>(out candle))
            {
                candle.BlowOutCandle();
            }
        }
        Object.Destroy(base.gameObject);
    }

    // Token: 0x0600020F RID: 527 RVA: 0x0000A1DF File Offset: 0x000083DF
    private void Delay()
    {
        Object.Destroy(base.gameObject);
    }

    // Token: 0x06000210 RID: 528 RVA: 0x0000A1EC File Offset: 0x000083EC
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Bullet"))
        {
            return;
        }
        this.collisions++;
        if (collision.collider.CompareTag("Enemy") && this.explodeOnTouch)
        {
            this.Explode();
        }
    }

    // Token: 0x06000211 RID: 529 RVA: 0x0000A23C File Offset: 0x0000843C
    private void Setup()
    {
        this.physics_mat = new PhysicMaterial();
        this.physics_mat.bounciness = this.bounciness;
        this.physics_mat.frictionCombine = PhysicMaterialCombine.Minimum;
        this.physics_mat.bounceCombine = PhysicMaterialCombine.Maximum;
        base.GetComponent<SphereCollider>().material = this.physics_mat;
        this.rb.useGravity = this.useGravity;
    }

    // Token: 0x06000212 RID: 530 RVA: 0x0000A29F File Offset: 0x0000849F
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.explosionRange);
    }

    // Token: 0x04000276 RID: 630
    public Rigidbody rb;

    // Token: 0x04000277 RID: 631
    public GameObject explosion;

    // Token: 0x04000278 RID: 632
    public LayerMask whatIsEnemies;

    // Token: 0x04000279 RID: 633
    [Range(0f, 1f)]
    public float bounciness;

    // Token: 0x0400027A RID: 634
    public bool useGravity;

    // Token: 0x0400027B RID: 635
    public int explosionDamage;

    // Token: 0x0400027C RID: 636
    public float explosionRange;

    // Token: 0x0400027D RID: 637
    public float explosionForce;

    // Token: 0x0400027E RID: 638
    public int maxCollisions;

    // Token: 0x0400027F RID: 639
    public float maxLifetime;

    // Token: 0x04000280 RID: 640
    public bool explodeOnTouch = true;

    // Token: 0x04000281 RID: 641
    private int collisions;

    // Token: 0x04000282 RID: 642
    private PhysicMaterial physics_mat;
}

using System;
using UnityEngine;

// Token: 0x02000009 RID: 9
public class CustomHint : MonoBehaviour
{
    // Token: 0x0600001B RID: 27 RVA: 0x000024DC File Offset: 0x000006DC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.hint.SetActive(true);
        }
    }

    // Token: 0x0600001C RID: 28 RVA: 0x000024F7 File Offset: 0x000006F7
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.hint.SetActive(false);
        }
    }

    // Token: 0x0600001D RID: 29 RVA: 0x00002512 File Offset: 0x00000712
    public void DeactivateTotally()
    {
        this.hint.SetActive(false);
        base.enabled = false;
    }

    // Token: 0x04000017 RID: 23
    public GameObject hint;
}

using System;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x0200001E RID: 30
public class CutoffMaskUI : Image
{
    // Token: 0x1700000C RID: 12
    // (get) Token: 0x0600007D RID: 125 RVA: 0x00003666 File Offset: 0x00001866
    public override Material materialForRendering
    {
        get
        {
            Material material = new Material(base.materialForRendering);
            material.SetInt("_StencilComp", 6);
            return material;
        }
    }
}

using System;
using UnityEngine;

// Token: 0x02000047 RID: 71
public class CutsceneChecker : MonoBehaviour
{
    // Token: 0x06000170 RID: 368 RVA: 0x000072C0 File Offset: 0x000054C0
    private void Start()
    {
        this._hasCompletedCutscene = PlayerPrefs.GetString("cutscene", "false");
        this._hasMetBooBoss = PlayerPrefs.GetString("hasMetBooBoss", "false");
        this._hasSeenTip = PlayerPrefs.GetString("hasSeenTip", "false");
        if (this._hasCompletedCutscene == "false")
        {
            this.player.SetActive(false);
            this.cutsceneObject.SetActive(true);
        }
        else if (this._hasCompletedCutscene == "true")
        {
            this.cutsceneObject.SetActive(false);
            this.player.SetActive(true);
        }
        this.boss.hasCompletedCutscene = (this._hasMetBooBoss == "true");
        this.boss.hasSeenTip = (this._hasSeenTip == "true");
    }

    // Token: 0x06000171 RID: 369 RVA: 0x000073A4 File Offset: 0x000055A4
    public void CompleteCutscene()
    {
        this._hasCompletedCutscene = "true";
        PlayerPrefs.SetString("cutscene", "true");
    }

    // Token: 0x06000172 RID: 370 RVA: 0x000073C0 File Offset: 0x000055C0
    public void MeetBooBoss()
    {
        this._hasMetBooBoss = "true";
        PlayerPrefs.SetString("hasMetBooBoss", "true");
    }

    // Token: 0x06000173 RID: 371 RVA: 0x000073DC File Offset: 0x000055DC
    public void HasSeenTip()
    {
        this._hasSeenTip = "true";
        PlayerPrefs.SetString("hasSeenTip", "true");
    }

    // Token: 0x040001B4 RID: 436
    [SerializeField]
    private GameObject cutsceneObject;

    // Token: 0x040001B5 RID: 437
    [SerializeField]
    private GameObject player;

    // Token: 0x040001B6 RID: 438
    [SerializeField]
    private BooBoss boss;

    // Token: 0x040001B7 RID: 439
    private string _hasCompletedCutscene;

    // Token: 0x040001B8 RID: 440
    private string _hasMetBooBoss;

    // Token: 0x040001B9 RID: 441
    private string _hasSeenTip;
}

using System;
using UnityEngine;

// Token: 0x02000068 RID: 104
public class DamageDealer : MonoBehaviour
{
    // Token: 0x06000214 RID: 532 RVA: 0x0000A2D0 File Offset: 0x000084D0
    private void OnTriggerEnter(Collider other)
    {
        PlayerHealth playerHealth;
        if (other.transform.root.TryGetComponent<PlayerHealth>(out playerHealth))
        {
            playerHealth.TakeDamage(this.damage);
        }
    }

    // Token: 0x06000215 RID: 533 RVA: 0x0000A300 File Offset: 0x00008500
    private void OnParticleCollision(GameObject other)
    {
        PlayerHealth playerHealth;
        if (other.transform.root.TryGetComponent<PlayerHealth>(out playerHealth))
        {
            playerHealth.TakeDamage(this.damage);
        }
    }

    // Token: 0x04000283 RID: 643
    public float damage;
}

using System;
using MEET_AND_TALK;
using TMPro;
using UnityEngine;

// Token: 0x02000075 RID: 117
[RequireComponent(typeof(CharacterController))]
public class DemoPlayer : MonoBehaviour
{
    // Token: 0x0600023D RID: 573 RVA: 0x0000AB4A File Offset: 0x00008D4A
    private void Start()
    {
        this.characterController = base.GetComponent<CharacterController>();
    }

    // Token: 0x0600023E RID: 574 RVA: 0x0000AB58 File Offset: 0x00008D58
    public void SetupInteractable(bool mode)
    {
        this.Interactable = mode;
    }

    // Token: 0x0600023F RID: 575 RVA: 0x0000AB64 File Offset: 0x00008D64
    private void Update()
    {
        if (this.Interactable)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Vector3 a = base.transform.TransformDirection(Vector3.forward);
            Vector3 a2 = base.transform.TransformDirection(Vector3.right);
            bool key = Input.GetKey(KeyCode.LeftShift);
            float d = this.canMove ? ((key ? this.runSpeed : this.walkSpeed) * Input.GetAxis("Vertical")) : 0f;
            float d2 = this.canMove ? ((key ? this.runSpeed : this.walkSpeed) * Input.GetAxis("Horizontal")) : 0f;
            float y = this.moveDirection.y;
            this.moveDirection = a * d + a2 * d2;
            if (Input.GetButton("Jump") && this.canMove && this.characterController.isGrounded)
            {
                this.moveDirection.y = this.jumpPower;
            }
            else
            {
                this.moveDirection.y = y;
            }
            if (!this.characterController.isGrounded)
            {
                this.moveDirection.y = this.moveDirection.y - this.gravity * Time.deltaTime;
            }
            this.characterController.Move(this.moveDirection * Time.deltaTime);
            if (this.canMove)
            {
                this.rotationX += -Input.GetAxis("Mouse Y") * this.lookSpeed;
                this.rotationX = Mathf.Clamp(this.rotationX, -this.lookXLimit, this.lookXLimit);
                this.playerCamera.transform.localRotation = Quaternion.Euler(this.rotationX, 0f, 0f);
                base.transform.rotation *= Quaternion.Euler(0f, Input.GetAxis("Mouse X") * this.lookSpeed, 0f);
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        this.Lanuage.text = string.Format("Language: {0}", this.localizationManager.SelectedLang());
        if (Input.GetKeyDown(KeyCode.U))
        {
            this.localizationManager.selectedLang = SystemLanguage.English;
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            this.localizationManager.selectedLang = SystemLanguage.Polish;
        }
    }

    // Token: 0x040002B0 RID: 688
    public LocalizationManager localizationManager;

    // Token: 0x040002B1 RID: 689
    public bool Interactable;

    // Token: 0x040002B2 RID: 690
    public Camera playerCamera;

    // Token: 0x040002B3 RID: 691
    public float walkSpeed = 6f;

    // Token: 0x040002B4 RID: 692
    public float runSpeed = 12f;

    // Token: 0x040002B5 RID: 693
    public float jumpPower = 7f;

    // Token: 0x040002B6 RID: 694
    public float gravity = 10f;

    // Token: 0x040002B7 RID: 695
    public TMP_Text Lanuage;

    // Token: 0x040002B8 RID: 696
    public float lookSpeed = 2f;

    // Token: 0x040002B9 RID: 697
    public float lookXLimit = 45f;

    // Token: 0x040002BA RID: 698
    private Vector3 moveDirection = Vector3.zero;

    // Token: 0x040002BB RID: 699
    private float rotationX;

    // Token: 0x040002BC RID: 700
    public bool canMove = true;

    // Token: 0x040002BD RID: 701
    private CharacterController characterController;
}

using System;
using UnityEngine;

// Token: 0x02000029 RID: 41
public class Destroyer : MonoBehaviour
{
    // Token: 0x060000E2 RID: 226 RVA: 0x000051BB File Offset: 0x000033BB
    private void Start()
    {
        Object.Destroy(base.gameObject, this.lifetime);
    }

    // Token: 0x040000F8 RID: 248
    public float lifetime;
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// Token: 0x0200001F RID: 31
public class DialogueSwitch : MonoBehaviour
{
    // Token: 0x0600007F RID: 127 RVA: 0x00003688 File Offset: 0x00001888
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (this.firstTime)
            {
                this.dialogues[0].Invoke();
                this.firstTime = false;
                return;
            }
            if (this.collector.items < 20)
            {
                this.dialogues[1].Invoke();
                return;
            }
            if (this.collector.items == 20)
            {
                this.dialogues[2].Invoke();
                base.Invoke("MakeAFace", 3.3f);
                return;
            }
            if (this.collector.items > 20)
            {
                this.dialogues[3].Invoke();
            }
        }
    }

    // Token: 0x06000080 RID: 128 RVA: 0x00003728 File Offset: 0x00001928
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.skip.Invoke();
        }
    }

    // Token: 0x06000081 RID: 129 RVA: 0x00003744 File Offset: 0x00001944
    private void MakeAFace()
    {
        this.trollAnimator.SetBool("hasChanged", true);
        this.fenceAnimator.SetBool("hasTrolled", true);
        this.tableText.text = "вход = 21";
        base.GetComponent<SphereCollider>().enabled = false;
        base.Invoke("StartFight", 4f);
    }

    // Token: 0x06000082 RID: 130 RVA: 0x0000379F File Offset: 0x0000199F
    private void StartFight()
    {
        base.GetComponent<TrollThrower>().enabled = true;
        if (this.tip != null)
        {
            this.tip.SetActive(true);
        }
    }

    // Token: 0x06000083 RID: 131 RVA: 0x000037C7 File Offset: 0x000019C7
    private void OnDestroy()
    {
        this.flower.SetActive(true);
    }

    // Token: 0x04000067 RID: 103
    public bool firstTime;

    // Token: 0x04000068 RID: 104
    public ItemCollector collector;

    // Token: 0x04000069 RID: 105
    public UnityEvent[] dialogues;

    // Token: 0x0400006A RID: 106
    public UnityEvent skip;

    // Token: 0x0400006B RID: 107
    public Animator trollAnimator;

    // Token: 0x0400006C RID: 108
    public Animator fenceAnimator;

    // Token: 0x0400006D RID: 109
    public TextMeshPro tableText;

    // Token: 0x0400006E RID: 110
    public GameObject flower;

    // Token: 0x0400006F RID: 111
    public GameObject tip;
}

using System;
using UnityEngine;

// Token: 0x0200000A RID: 10
public class DomeMelonController : MonoBehaviour
{
    // Token: 0x0600001F RID: 31 RVA: 0x00002530 File Offset: 0x00000730
    private void OnEnable()
    {
        MelonfallManager melonfallManager = this.melonfall;
        melonfallManager.hasCompletedMelonfall = (Action)Delegate.Combine(melonfallManager.hasCompletedMelonfall, new Action(this.OnMelonfallFinished));
        CurveSpawner curveSpawner = this.curveSpawner;
        curveSpawner.hasDoneSpawning = (Action)Delegate.Combine(curveSpawner.hasDoneSpawning, new Action(this.OnSpawnerFinished));
    }

    // Token: 0x06000020 RID: 32 RVA: 0x0000258C File Offset: 0x0000078C
    private void OnDisable()
    {
        MelonfallManager melonfallManager = this.melonfall;
        melonfallManager.hasCompletedMelonfall = (Action)Delegate.Remove(melonfallManager.hasCompletedMelonfall, new Action(this.OnMelonfallFinished));
        CurveSpawner curveSpawner = this.curveSpawner;
        curveSpawner.hasDoneSpawning = (Action)Delegate.Remove(curveSpawner.hasDoneSpawning, new Action(this.OnSpawnerFinished));
    }

    // Token: 0x06000021 RID: 33 RVA: 0x000025E7 File Offset: 0x000007E7
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !this._hasSteppedInCenter)
        {
            this._hasSteppedInCenter = true;
            this.floorGlassAnimator.SetTrigger("hasStepped");
            GravityController.instance.SetEarthGravity();
        }
    }

    // Token: 0x06000022 RID: 34 RVA: 0x0000261F File Offset: 0x0000081F
    public void OnMelonfallFinished()
    {
        this.curveSpawner.enabled = true;
    }

    // Token: 0x06000023 RID: 35 RVA: 0x0000262D File Offset: 0x0000082D
    public void OnSpawnerFinished()
    {
        this.floorCylinderCollider.enabled = false;
        this.sign.SetActive(true);
        this.floorGlassAnimator.SetTrigger("hasFinished");
        MusicManager.instance.EnableMusic(0);
        base.enabled = false;
    }

    // Token: 0x04000018 RID: 24
    [SerializeField]
    private MelonfallManager melonfall;

    // Token: 0x04000019 RID: 25
    [SerializeField]
    private CurveSpawner curveSpawner;

    // Token: 0x0400001A RID: 26
    [SerializeField]
    private Collider floorCylinderCollider;

    // Token: 0x0400001B RID: 27
    [SerializeField]
    private Animator floorGlassAnimator;

    // Token: 0x0400001C RID: 28
    [SerializeField]
    private GameObject sign;

    // Token: 0x0400001D RID: 29
    private bool _hasSteppedInCenter;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x0200002A RID: 42
public class DoorInteraction : MonoBehaviour
{
    // Token: 0x060000E4 RID: 228 RVA: 0x000051D6 File Offset: 0x000033D6
    private void Update()
    {
        this.playerNearDoor = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        if (this.playerNearDoor && Input.GetKeyDown(KeyCode.E))
        {
            this.OpenDoor();
        }
    }

    // Token: 0x060000E5 RID: 229 RVA: 0x00005218 File Offset: 0x00003418
    public void OpenDoor()
    {
        this.doorAnimator.SetBool("isOpened", true);
        this.gateAnimator.SetBool("hasMushrooms", true);
        this.enemyRun.speed = 8f;
        this.enemyRun.GetComponent<AudioSource>().Play();
        Object.FindObjectOfType<MusicManager>().EnableMusic(3);
        base.enabled = false;
    }

    // Token: 0x060000E6 RID: 230 RVA: 0x00005279 File Offset: 0x00003479
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x040000F9 RID: 249
    public Animator doorAnimator;

    // Token: 0x040000FA RID: 250
    public Animator gateAnimator;

    // Token: 0x040000FB RID: 251
    public NavMeshAgent enemyRun;

    // Token: 0x040000FC RID: 252
    public LayerMask whatIsPlayer;

    // Token: 0x040000FD RID: 253
    public float sightRange;

    // Token: 0x040000FE RID: 254
    private bool playerNearDoor;
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Token: 0x02000011 RID: 17
public class DynamicJoystick : Joystick
{
    // Token: 0x17000009 RID: 9
    // (get) Token: 0x06000049 RID: 73 RVA: 0x00002D1C File Offset: 0x00000F1C
    // (set) Token: 0x0600004A RID: 74 RVA: 0x00002D24 File Offset: 0x00000F24
    public float MoveThreshold
    {
        get
        {
            return this.moveThreshold;
        }
        set
        {
            this.moveThreshold = Mathf.Abs(value);
        }
    }

    // Token: 0x0600004B RID: 75 RVA: 0x00002D32 File Offset: 0x00000F32
    protected override void Start()
    {
        this.MoveThreshold = this.moveThreshold;
        base.Start();
        this.background.gameObject.SetActive(false);
    }

    // Token: 0x0600004C RID: 76 RVA: 0x00002D57 File Offset: 0x00000F57
    public override void OnPointerDown(PointerEventData eventData)
    {
        this.background.anchoredPosition = base.ScreenPointToAnchoredPosition(eventData.position);
        this.background.gameObject.SetActive(true);
        base.OnPointerDown(eventData);
    }

    // Token: 0x0600004D RID: 77 RVA: 0x00002D88 File Offset: 0x00000F88
    public override void OnPointerUp(PointerEventData eventData)
    {
        this.background.gameObject.SetActive(false);
        base.OnPointerUp(eventData);
    }

    // Token: 0x0600004E RID: 78 RVA: 0x00002DA4 File Offset: 0x00000FA4
    protected override void HandleInput(float magnitude, Vector2 normalised, Vector2 radius, Camera cam)
    {
        if (magnitude > this.moveThreshold)
        {
            Vector2 b = normalised * (magnitude - this.moveThreshold) * radius;
            this.background.anchoredPosition += b;
        }
        base.HandleInput(magnitude, normalised, radius, cam);
    }

    // Token: 0x0400003C RID: 60
    [SerializeField]
    private float moveThreshold = 1f;
}

using System;
using UnityEngine;

// Token: 0x02000074 RID: 116
[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class EnableDepthBuffer : MonoBehaviour
{
    // Token: 0x0600023B RID: 571 RVA: 0x0000AB0D File Offset: 0x00008D0D
    private void Update()
    {
        if (this.m_camera == null)
        {
            this.m_camera = base.GetComponent<Camera>();
        }
        if (this.m_camera.depthTextureMode == DepthTextureMode.None)
        {
            this.m_camera.depthTextureMode = DepthTextureMode.Depth;
        }
    }

    // Token: 0x040002AF RID: 687
    private Camera m_camera;
}

using System;
using UnityEngine;

// Token: 0x0200002B RID: 43
public class EndGame : MonoBehaviour
{
    // Token: 0x060000E8 RID: 232 RVA: 0x000052A4 File Offset: 0x000034A4
    public void FadeToTitles()
    {
        this.magicPickSound.Play();
        MusicManager.instance.EnableMusic(6);
        base.gameObject.SetActive(false);
        AfterTitles[] array = Object.FindObjectsOfType<AfterTitles>(true);
        for (int i = 0; i < array.Length; i++)
        {
            array[i].gameObject.SetActive(true);
        }
    }

    // Token: 0x040000FF RID: 255
    public AudioSource magicPickSound;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x02000024 RID: 36
public class Enemy : MonoBehaviour
{
    // Token: 0x060000BD RID: 189 RVA: 0x0000479D File Offset: 0x0000299D
    private void Awake()
    {
        this.player = GameObject.FindGameObjectWithTag("Player").transform.root;
        this.agent = base.GetComponent<NavMeshAgent>();
    }

    // Token: 0x060000BE RID: 190 RVA: 0x000047C8 File Offset: 0x000029C8
    private void Update()
    {
        this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
        if (!this.playerInSightRange && !this.playerInAttackRange)
        {
            this.Patroling();
        }
        if (this.playerInSightRange && !this.playerInAttackRange)
        {
            this.ChasePlayer();
        }
        if (this.playerInAttackRange && this.playerInSightRange)
        {
            this.AttackPlayer();
        }
    }

    // Token: 0x060000BF RID: 191 RVA: 0x00004868 File Offset: 0x00002A68
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x060000C0 RID: 192 RVA: 0x000048CC File Offset: 0x00002ACC
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x060000C1 RID: 193 RVA: 0x00004974 File Offset: 0x00002B74
    private void ChasePlayer()
    {
        if (Vector3.Distance(this.player.position, this.lastPlayerPosition) > 0.5f)
        {
            this.agent.SetDestination(this.player.position);
            this.lastPlayerPosition = this.player.position;
        }
    }

    // Token: 0x060000C2 RID: 194 RVA: 0x000049C8 File Offset: 0x00002BC8
    private void AttackPlayer()
    {
        if (Vector3.Distance(this.player.position, this.lastPlayerPosition) > 0.5f)
        {
            this.agent.SetDestination(this.player.position);
            this.lastPlayerPosition = this.player.position;
        }
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x060000C3 RID: 195 RVA: 0x00004A91 File Offset: 0x00002C91
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x060000C4 RID: 196 RVA: 0x00004A9A File Offset: 0x00002C9A
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x060000C5 RID: 197 RVA: 0x00004AC8 File Offset: 0x00002CC8
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x060000C6 RID: 198 RVA: 0x00004AF4 File Offset: 0x00002CF4
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x040000C0 RID: 192
    public NavMeshAgent agent;

    // Token: 0x040000C1 RID: 193
    public Transform player;

    // Token: 0x040000C2 RID: 194
    public LayerMask whatIsGround;

    // Token: 0x040000C3 RID: 195
    public LayerMask whatIsPlayer;

    // Token: 0x040000C4 RID: 196
    public Vector3 walkPoint;

    // Token: 0x040000C5 RID: 197
    private bool walkPointSet;

    // Token: 0x040000C6 RID: 198
    public float walkPointRange;

    // Token: 0x040000C7 RID: 199
    public float timeBetweenAttacks;

    // Token: 0x040000C8 RID: 200
    private bool alreadyAttacked;

    // Token: 0x040000C9 RID: 201
    public float sightRange;

    // Token: 0x040000CA RID: 202
    public float attackRange;

    // Token: 0x040000CB RID: 203
    public bool playerInSightRange;

    // Token: 0x040000CC RID: 204
    public bool playerInAttackRange;

    // Token: 0x040000CD RID: 205
    public float health;

    // Token: 0x040000CE RID: 206
    public int damage;

    // Token: 0x040000CF RID: 207
    public GameObject deathEffect;

    // Token: 0x040000D0 RID: 208
    private Vector3 lastPlayerPosition;
}

using System;
using UnityEngine;

// Token: 0x02000025 RID: 37
public class EnemySpawner : MonoBehaviour
{
    // Token: 0x060000C8 RID: 200 RVA: 0x00004B4C File Offset: 0x00002D4C
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            int num = Random.Range(0, this.spawnPoints.Length);
            int num2 = Random.Range(0, this.enemies.Length);
            Object.Instantiate<GameObject>(this.enemies[num2], this.spawnPoints[num].position, Quaternion.identity);
            base.enabled = false;
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x040000D1 RID: 209
    public Transform[] spawnPoints;

    // Token: 0x040000D2 RID: 210
    public GameObject[] enemies;
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Token: 0x02000071 RID: 113
public class EnvMapAnimator : MonoBehaviour
{
    // Token: 0x06000233 RID: 563 RVA: 0x0000A992 File Offset: 0x00008B92
    private void Awake()
    {
        this.m_textMeshPro = base.GetComponent<TMP_Text>();
        this.m_material = this.m_textMeshPro.fontSharedMaterial;
    }

    // Token: 0x06000234 RID: 564 RVA: 0x0000A9B1 File Offset: 0x00008BB1
    private IEnumerator Start()
    {
        Matrix4x4 matrix = default(Matrix4x4);
        for (; ; )
        {
            matrix.SetTRS(Vector3.zero, Quaternion.Euler(Time.time * this.RotationSpeeds.x, Time.time * this.RotationSpeeds.y, Time.time * this.RotationSpeeds.z), Vector3.one);
            this.m_material.SetMatrix("_EnvMatrix", matrix);
            yield return null;
        }
        yield break;
    }

    // Token: 0x040002A5 RID: 677
    public Vector3 RotationSpeeds;

    // Token: 0x040002A6 RID: 678
    private TMP_Text m_textMeshPro;

    // Token: 0x040002A7 RID: 679
    private Material m_material;
}

using System;

// Token: 0x02000012 RID: 18
public class FixedJoystick : Joystick
{
}

using System;
using UnityEngine.EventSystems;

// Token: 0x02000013 RID: 19
public class FloatingJoystick : Joystick
{
    // Token: 0x06000051 RID: 81 RVA: 0x00002E0C File Offset: 0x0000100C
    protected override void Start()
    {
        base.Start();
        this.background.gameObject.SetActive(false);
    }

    // Token: 0x06000052 RID: 82 RVA: 0x00002E25 File Offset: 0x00001025
    public override void OnPointerDown(PointerEventData eventData)
    {
        this.background.anchoredPosition = base.ScreenPointToAnchoredPosition(eventData.position);
        this.background.gameObject.SetActive(true);
        base.OnPointerDown(eventData);
    }

    // Token: 0x06000053 RID: 83 RVA: 0x00002E56 File Offset: 0x00001056
    public override void OnPointerUp(PointerEventData eventData)
    {
        this.background.gameObject.SetActive(false);
        base.OnPointerUp(eventData);
    }
}

using System;
using UnityEngine;

// Token: 0x0200003C RID: 60
public class FoodActivator : MonoBehaviour
{
    // Token: 0x06000146 RID: 326 RVA: 0x00006A5C File Offset: 0x00004C5C
    private void OnDestroy()
    {
        this.burger.StartCoroutine(this.burger.OnBurgerDestroyed());
    }

    // Token: 0x0400018E RID: 398
    public BurgerBehaviour burger;
}

using System;
using UnityEngine;
using UnityEngine.Events;

// Token: 0x02000048 RID: 72
public class FoxDialogueSwitch : MonoBehaviour
{
    // Token: 0x06000175 RID: 373 RVA: 0x00007400 File Offset: 0x00005600
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (!this.tierController.HasWon)
            {
                this.dialogues[0].Invoke();
                this.SwitchGateState(false);
                return;
            }
            if (this.tierController.HasWon && this.itemCollector.items >= 10)
            {
                this.dialogues[1].Invoke();
                this.SwitchGateState(true);
            }
        }
    }

    // Token: 0x06000176 RID: 374 RVA: 0x0000746C File Offset: 0x0000566C
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            this.skip.Invoke();
        }
    }

    // Token: 0x06000177 RID: 375 RVA: 0x00007482 File Offset: 0x00005682
    public void SwitchGateState(bool state)
    {
        this.gateAnimator.SetBool("isOpened", state);
    }

    // Token: 0x040001BA RID: 442
    [SerializeField]
    private FoxTierController tierController;

    // Token: 0x040001BB RID: 443
    [SerializeField]
    private UnityEvent[] dialogues;

    // Token: 0x040001BC RID: 444
    [SerializeField]
    private UnityEvent skip;

    // Token: 0x040001BD RID: 445
    [SerializeField]
    private Animator gateAnimator;

    // Token: 0x040001BE RID: 446
    [SerializeField]
    private ItemCollector itemCollector;
}

using System;
using UnityEngine;

// Token: 0x02000049 RID: 73
public class FoxShootable : MonoBehaviour
{
    // Token: 0x06000179 RID: 377 RVA: 0x0000749D File Offset: 0x0000569D
    private void Start()
    {
        this._foxTierController = Object.FindObjectOfType<FoxTierController>();
    }

    // Token: 0x0600017A RID: 378 RVA: 0x000074AA File Offset: 0x000056AA
    public void TakeDamage(int damage)
    {
        this.health -= damage;
        if (this.health <= 0)
        {
            this._foxTierController.DestroyFox(base.gameObject);
        }
    }

    // Token: 0x040001BF RID: 447
    [SerializeField]
    private int health;

    // Token: 0x040001C0 RID: 448
    public int FoxType;

    // Token: 0x040001C1 RID: 449
    private FoxTierController _foxTierController;
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Token: 0x0200004A RID: 74
public class FoxTierController : MonoBehaviour
{
    // Token: 0x0600017C RID: 380 RVA: 0x000074DC File Offset: 0x000056DC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.targetFoxType = 5;
            this.SpawnFoxes();
            this.hintText.text = "стреляйте только в указанных лисиц!";
            this.hintText.transform.root.GetComponent<Animator>().SetTrigger("isActivated");
            this.icon.SetActive(true);
        }
    }

    // Token: 0x0600017D RID: 381 RVA: 0x0000753E File Offset: 0x0000573E
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.icon.SetActive(false);
        }
    }

    // Token: 0x0600017E RID: 382 RVA: 0x0000755C File Offset: 0x0000575C
    public void SpawnFoxes()
    {
        this._targetFoxCount = 0;
        this.DestroyAllFoxes();
        this.spawnedFoxes = new List<GameObject>();
        this.targetFoxType = ((this.targetFoxType < 5) ? (this.targetFoxType + 1) : 0);
        GameObject[] array = this.targetFoxIcons;
        for (int i = 0; i < array.Length; i++)
        {
            array[i].SetActive(false);
        }
        this.targetFoxIcons[this.targetFoxType].SetActive(true);
        foreach (Transform transform in this.foxPositions)
        {
            int num = Random.Range(0, this.foxVariants.Length);
            FoxShootable component = Object.Instantiate<GameObject>(this.foxVariants[num], transform.position, transform.rotation).GetComponent<FoxShootable>();
            component.FoxType = num;
            if (num == this.targetFoxType)
            {
                this._targetFoxCount++;
            }
            this.spawnedFoxes.Add(component.gameObject);
        }
        this.targetFoxCountText.text = "x" + this._targetFoxCount.ToString();
        if (this._targetFoxCount == 0)
        {
            this.targetFoxType = ((this.targetFoxType > 0) ? (this.targetFoxType - 1) : 5);
            this.SpawnFoxes();
        }
    }

    // Token: 0x0600017F RID: 383 RVA: 0x00007694 File Offset: 0x00005894
    public void DestroyAllFoxes()
    {
        foreach (GameObject obj in this.spawnedFoxes)
        {
            Object.Destroy(obj);
        }
    }

    // Token: 0x06000180 RID: 384 RVA: 0x000076E4 File Offset: 0x000058E4
    public void DestroyFox(GameObject fox)
    {
        if (fox.GetComponent<FoxShootable>().FoxType == this.targetFoxType)
        {
            if (this._targetFoxCount > 1)
            {
                this._targetFoxCount--;
                this.targetFoxCountText.text = "x" + this._targetFoxCount.ToString();
            }
            else if (this.targetFoxType == 5)
            {
                this.hintText.text = "победа!";
                this._targetFoxCount--;
                this.targetFoxCountText.text = "x" + this._targetFoxCount.ToString();
                this.hintText.transform.root.GetComponent<Animator>().SetTrigger("isActivated");
                this.HasWon = true;
                this.DestroyAllFoxes();
                this.icon.SetActive(false);
                base.GetComponent<Collider>().enabled = false;
            }
            else
            {
                this.SpawnFoxes();
            }
        }
        else
        {
            this.targetFoxType = 5;
            this.hintText.text = "мимо! Попробуйте снова";
            this.hintText.transform.root.GetComponent<Animator>().SetTrigger("isActivated");
            this.SpawnFoxes();
        }
        if (this.spawnedFoxes.Contains(fox))
        {
            Object.Destroy(fox);
            this.spawnedFoxes.Remove(fox);
        }
    }

    // Token: 0x040001C2 RID: 450
    [SerializeField]
    private Transform[] foxPositions;

    // Token: 0x040001C3 RID: 451
    [SerializeField]
    private GameObject[] foxVariants;

    // Token: 0x040001C4 RID: 452
    [SerializeField]
    private GameObject[] targetFoxIcons;

    // Token: 0x040001C5 RID: 453
    [SerializeField]
    private GameObject icon;

    // Token: 0x040001C6 RID: 454
    [SerializeField]
    private List<GameObject> spawnedFoxes;

    // Token: 0x040001C7 RID: 455
    [SerializeField]
    private int targetFoxType;

    // Token: 0x040001C8 RID: 456
    [SerializeField]
    private TextMeshProUGUI targetFoxCountText;

    // Token: 0x040001C9 RID: 457
    [SerializeField]
    private TextMeshProUGUI hintText;

    // Token: 0x040001CA RID: 458
    public bool HasWon;

    // Token: 0x040001CB RID: 459
    private int _targetFoxCount;
}

using System;
using UnityEngine;

// Token: 0x0200004B RID: 75
public class FurnaceLighterTrigger : MonoBehaviour
{
    // Token: 0x06000182 RID: 386 RVA: 0x00007840 File Offset: 0x00005A40
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this._isInZone = true;
            this.hints[this._hasLit ? 1 : 0].SetActive(true);
            this.customHint.DeactivateTotally();
        }
    }

    // Token: 0x06000183 RID: 387 RVA: 0x0000787A File Offset: 0x00005A7A
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this._isInZone = false;
            this.hints[this._hasLit ? 1 : 0].SetActive(false);
        }
    }

    // Token: 0x06000184 RID: 388 RVA: 0x000078AC File Offset: 0x00005AAC
    private void Update()
    {
        if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return)) && this._isInZone)
        {
            if (this.collector.items > 0 && !this._hasLit)
            {
                this._hasLit = true;
                this.hints[0].SetActive(false);
                this.collector.items = 0;
                this.woodPanel.transform.GetChild(0).gameObject.SetActive(false);
                this.collector.itemText.text = string.Format("{0}", this.collector.items);
                this.collector.SaveMushrooms();
                this.fire.SetActive(true);
                this.colliders[0].enabled = false;
                this.colliders[1].enabled = true;
                return;
            }
            if (this._hasLit && !this._hasSat)
            {
                this._hasSat = true;
                this.player.isStopped = true;
                this.player.gameObject.SetActive(false);
                this.cutscene.SetActive(true);
                this.hints[1].SetActive(false);
            }
        }
    }

    // Token: 0x040001CC RID: 460
    [SerializeField]
    private GameObject[] hints;

    // Token: 0x040001CD RID: 461
    [SerializeField]
    private Collider[] colliders;

    // Token: 0x040001CE RID: 462
    [SerializeField]
    private GameObject fire;

    // Token: 0x040001CF RID: 463
    [SerializeField]
    private GameObject woodPanel;

    // Token: 0x040001D0 RID: 464
    [SerializeField]
    private GameObject cutscene;

    // Token: 0x040001D1 RID: 465
    [SerializeField]
    private PlayerController player;

    // Token: 0x040001D2 RID: 466
    [SerializeField]
    private ItemCollector collector;

    // Token: 0x040001D3 RID: 467
    [SerializeField]
    private CustomHint customHint;

    // Token: 0x040001D4 RID: 468
    private bool _isInZone;

    // Token: 0x040001D5 RID: 469
    public bool _hasLit;

    // Token: 0x040001D6 RID: 470
    public bool _hasSat;
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

// Token: 0x0200005E RID: 94
public class GameSettings : MonoBehaviour
{
    // Token: 0x060001DF RID: 479 RVA: 0x00009069 File Offset: 0x00007269
    private void Start()
    {
        this.LoadValues();
    }

    // Token: 0x060001E0 RID: 480 RVA: 0x00009071 File Offset: 0x00007271
    public void TextToSlider(float sensitivity)
    {
        this.sliderPercentUI.text = string.Format("чувствительность мыши: {0}%", Mathf.RoundToInt(sensitivity / 16f));
    }

    // Token: 0x060001E1 RID: 481 RVA: 0x00009099 File Offset: 0x00007299
    public void TickState()
    {
        if (this.postEffectsEnabled == 0)
        {
            this.tick.SetActive(false);
            this.postProcess.enabled = false;
            return;
        }
        this.tick.SetActive(true);
        this.postProcess.enabled = true;
    }

    // Token: 0x060001E2 RID: 482 RVA: 0x000090D4 File Offset: 0x000072D4
    public void SaveSensitivity()
    {
        float value = this.sensitivitySlider.value;
        PlayerPrefs.SetFloat("SensitivityValue", value);
        this.LoadValues();
    }

    // Token: 0x060001E3 RID: 483 RVA: 0x00009100 File Offset: 0x00007300
    public void SaveEffects()
    {
        if (this.tick.activeSelf)
        {
            this.tick.SetActive(false);
            this.postEffectsEnabled = 0;
        }
        else
        {
            this.tick.SetActive(true);
            this.postEffectsEnabled = 1;
        }
        PlayerPrefs.SetInt("PostEffects", this.postEffectsEnabled);
        this.LoadValues();
    }

    // Token: 0x060001E4 RID: 484 RVA: 0x00009158 File Offset: 0x00007358
    public void SaveQuality(int index)
    {
        this.graphicsQuality = index;
        PlayerPrefs.SetInt("GraphicsQuality", this.graphicsQuality);
    }

    // Token: 0x060001E5 RID: 485 RVA: 0x00009174 File Offset: 0x00007374
    public void LoadValues()
    {
        float num = 400f;
        int num2 = 1;
        int qualityLevel = 2;
        if (PlayerPrefs.HasKey("SensitivityValue"))
        {
            num = PlayerPrefs.GetFloat("SensitivityValue");
        }
        if (PlayerPrefs.HasKey("PostEffects"))
        {
            num2 = PlayerPrefs.GetInt("PostEffects");
        }
        if (PlayerPrefs.HasKey("GraphicsQuality"))
        {
            qualityLevel = PlayerPrefs.GetInt("GraphicsQuality");
        }
        this.sensitivitySlider.value = num;
        this.postEffectsEnabled = num2;
        this.graphicsQuality = qualityLevel;
        this.qualityDropdown.SetQualityLevel(qualityLevel);
        this.TextToSlider(num);
        this.TickState();
        this.playerCamera.sensitivityX = (this.playerCamera.sensitivityY = num);
    }

    // Token: 0x04000252 RID: 594
    [SerializeField]
    private Slider sensitivitySlider;

    // Token: 0x04000253 RID: 595
    [SerializeField]
    private PostProcessLayer postProcess;

    // Token: 0x04000254 RID: 596
    [SerializeField]
    private PlayerCamera playerCamera;

    // Token: 0x04000255 RID: 597
    [SerializeField]
    private TextMeshProUGUI sliderPercentUI;

    // Token: 0x04000256 RID: 598
    [SerializeField]
    private SetQuality qualityDropdown;

    // Token: 0x04000257 RID: 599
    public int postEffectsEnabled;

    // Token: 0x04000258 RID: 600
    public int graphicsQuality;

    // Token: 0x04000259 RID: 601
    [SerializeField]
    private GameObject tick;
}

using System;
using UnityEngine;

// Token: 0x0200003D RID: 61
public class GIFController : MonoBehaviour
{
    // Token: 0x06000148 RID: 328 RVA: 0x00006A7D File Offset: 0x00004C7D
    private void Start()
    {
    }

    // Token: 0x06000149 RID: 329 RVA: 0x00006A7F File Offset: 0x00004C7F
    private void Update()
    {
    }
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Token: 0x0200002F RID: 47
public class GravityController : MonoBehaviour
{
    // Token: 0x060000F4 RID: 244 RVA: 0x00005455 File Offset: 0x00003655
    private void Awake()
    {
        if (GravityController.instance != null)
        {
            Object.Destroy(base.gameObject);
            return;
        }
        GravityController.instance = this;
        Object.DontDestroyOnLoad(base.transform.gameObject);
    }

    // Token: 0x060000F5 RID: 245 RVA: 0x00005488 File Offset: 0x00003688
    public void CheckGravity()
    {
        if (SceneManager.GetActiveScene().buildIndex == 2)
        {
            this.SetMoonGravity();
            return;
        }
        if (SceneManager.GetActiveScene().buildIndex == 1)
        {
            this.SetEarthGravity();
        }
    }

    // Token: 0x060000F6 RID: 246 RVA: 0x000054C2 File Offset: 0x000036C2
    public void SetMoonGravity()
    {
        Physics.gravity = this.moonGravity;
        this.player.jumpForce = this.moonJumpForce;
    }

    // Token: 0x060000F7 RID: 247 RVA: 0x000054E0 File Offset: 0x000036E0
    public void SetEarthGravity()
    {
        Physics.gravity = this.earthGravity;
        this.player.jumpForce = this.earthJumpForce;
    }

    // Token: 0x04000109 RID: 265
    public static GravityController instance;

    // Token: 0x0400010A RID: 266
    [SerializeField]
    private Vector3 moonGravity;

    // Token: 0x0400010B RID: 267
    [SerializeField]
    private Vector3 earthGravity;

    // Token: 0x0400010C RID: 268
    public PlayerController player;

    // Token: 0x0400010D RID: 269
    [SerializeField]
    private float earthJumpForce;

    // Token: 0x0400010E RID: 270
    [SerializeField]
    private float moonJumpForce;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x0200002C RID: 44
public class GribmanActivator : MonoBehaviour
{
    // Token: 0x060000EA RID: 234 RVA: 0x000052FE File Offset: 0x000034FE
    private void Start()
    {
        Object.Instantiate<GameObject>(this.gribmanSound, base.transform.position, Quaternion.identity);
        base.Invoke("AttackPlayer", 8.5f);
    }

    // Token: 0x060000EB RID: 235 RVA: 0x0000532C File Offset: 0x0000352C
    public void AttackPlayer()
    {
        base.GetComponent<NavMeshAgent>().speed = 10f;
        Object.Instantiate<GameObject>(this.attackSound, base.transform.position, Quaternion.identity);
        base.Invoke("AttackPlayer", 8.5f);
    }

    // Token: 0x04000100 RID: 256
    public GameObject gribmanSound;

    // Token: 0x04000101 RID: 257
    public GameObject attackSound;
}

using System;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x0200000B RID: 11
public class GridLayoutManager : MonoBehaviour
{
    // Token: 0x06000025 RID: 37 RVA: 0x00002671 File Offset: 0x00000871
    private void Start()
    {
        this.ConfigureGrid(2, 4, 150f, 150f, 10f, 10f);
    }

    // Token: 0x06000026 RID: 38 RVA: 0x00002690 File Offset: 0x00000890
    public void ConfigureGrid(int gridRows, int gridCols, float cellWidth, float cellHeight, float spacingX, float spacingY)
    {
        if (this.gridLayoutGroup == null)
        {
            Debug.LogError("GridLayoutGroup is not assigned!");
            return;
        }
        this.gridLayoutGroup.cellSize = new Vector2(cellWidth, cellHeight);
        this.gridLayoutGroup.spacing = new Vector2(spacingX, spacingY);
        float x = (float)gridCols * cellWidth + (float)(gridCols - 1) * spacingX;
        float y = (float)gridRows * cellHeight + (float)(gridRows - 1) * spacingY;
        this.cardParentRect.sizeDelta = new Vector2(x, y);
        this.gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        this.gridLayoutGroup.constraintCount = gridCols;
    }

    // Token: 0x0400001E RID: 30
    public GridLayoutGroup gridLayoutGroup;

    // Token: 0x0400001F RID: 31
    public int gridRows = 2;

    // Token: 0x04000020 RID: 32
    public int gridCols = 4;

    // Token: 0x04000021 RID: 33
    public float cellWidth = 150f;

    // Token: 0x04000022 RID: 34
    public float cellHeight = 150f;

    // Token: 0x04000023 RID: 35
    public float spacingX = 10f;

    // Token: 0x04000024 RID: 36
    public float spacingY = 10f;

    // Token: 0x04000025 RID: 37
    public RectTransform cardParentRect;
}

using System;
using UnityEngine;

// Token: 0x02000069 RID: 105
public class GunPickup : MonoBehaviour
{
    // Token: 0x06000217 RID: 535 RVA: 0x0000A335 File Offset: 0x00008535
    private void Start()
    {
        if (PlayerPrefs.GetString("Gun", "false") == "true")
        {
            base.gameObject.SetActive(false);
        }
    }

    // Token: 0x06000218 RID: 536 RVA: 0x0000A360 File Offset: 0x00008560
    public void PickupGun()
    {
        this.Gun.SetActive(true);
        this.Aim.SetActive(true);
        this.GunText.SetActive(true);
        if (this.Sound != null)
        {
            Object.Instantiate<GameObject>(this.Sound, base.transform.position, Quaternion.identity);
        }
        this.player.hasGun = "true";
        Object.Destroy(base.gameObject);
    }

    // Token: 0x04000284 RID: 644
    public GameObject Gun;

    // Token: 0x04000285 RID: 645
    public GameObject Aim;

    // Token: 0x04000286 RID: 646
    public GameObject GunText;

    // Token: 0x04000287 RID: 647
    public GameObject Sound;

    // Token: 0x04000288 RID: 648
    public PlayerController player;
}

using System;
using UnityEngine;

// Token: 0x0200000C RID: 12
public class HorrorSoundTrigger : MonoBehaviour
{
    // Token: 0x06000028 RID: 40 RVA: 0x00002771 File Offset: 0x00000971
    private void Start()
    {
    }

    // Token: 0x06000029 RID: 41 RVA: 0x00002773 File Offset: 0x00000973
    private void Update()
    {
    }
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

// Token: 0x02000031 RID: 49
public class Intro : MonoBehaviour
{
    // Token: 0x06000103 RID: 259 RVA: 0x00005908 File Offset: 0x00003B08
    private void Awake()
    {
        this.LoadIntro();
    }

    // Token: 0x06000104 RID: 260 RVA: 0x00005910 File Offset: 0x00003B10
    private void Start()
    {
        if (this.hasWatched == "false")
        {
            base.StartCoroutine(this.WatchVid());
            return;
        }
        this.videoClip.gameObject.SetActive(false);
    }

    // Token: 0x06000105 RID: 261 RVA: 0x00005943 File Offset: 0x00003B43
    private IEnumerator WatchVid()
    {
        PlayerController player = Object.FindObjectOfType<PlayerController>();
        player.isStopped = true;
        this.videoClip.gameObject.SetActive(true);
        this.videoClip.Play();
        yield return new WaitForSeconds(38.1f);
        this.hasWatched = "true";
        this.videoClip.gameObject.SetActive(false);
        player.isStopped = false;
        this.SaveIntro();
        yield break;
    }

    // Token: 0x06000106 RID: 262 RVA: 0x00005952 File Offset: 0x00003B52
    private void SaveIntro()
    {
        PlayerPrefs.SetString("Intro", this.hasWatched);
    }

    // Token: 0x06000107 RID: 263 RVA: 0x00005964 File Offset: 0x00003B64
    private void LoadIntro()
    {
        this.hasWatched = PlayerPrefs.GetString("Intro", "false");
    }

    // Token: 0x04000127 RID: 295
    public string hasWatched = "false";

    // Token: 0x04000128 RID: 296
    public VideoPlayer videoClip;
}

using System;
using UnityEngine;

// Token: 0x02000033 RID: 51
public class Item : MonoBehaviour
{
    // Token: 0x0600010C RID: 268 RVA: 0x0000599A File Offset: 0x00003B9A
    private void Start()
    {
        if (!this.isFinalMushroom)
        {
            this.LoadPresence();
        }
    }

    // Token: 0x0600010D RID: 269 RVA: 0x000059AC File Offset: 0x00003BAC
    public void OnCollect()
    {
        this.isCollected = "yes";
        if (this.pickSound != null)
        {
            Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
        }
        if (this.isFinalMushroom)
        {
            base.GetComponent<ShaiActivator>().OnPick();
        }
        base.gameObject.SetActive(false);
    }

    // Token: 0x0600010E RID: 270 RVA: 0x00005A0D File Offset: 0x00003C0D
    public void SavePresence()
    {
        PlayerPrefs.SetString("isCollected" + base.gameObject.name, this.isCollected);
    }

    // Token: 0x0600010F RID: 271 RVA: 0x00005A2F File Offset: 0x00003C2F
    public void LoadPresence()
    {
        this.isCollected = PlayerPrefs.GetString("isCollected" + base.gameObject.name);
        this.CheckPresence();
    }

    // Token: 0x06000110 RID: 272 RVA: 0x00005A57 File Offset: 0x00003C57
    public void CheckPresence()
    {
        if (this.isCollected == "yes")
        {
            base.gameObject.SetActive(false);
        }
    }

    // Token: 0x04000129 RID: 297
    public string isCollected;

    // Token: 0x0400012A RID: 298
    public GameObject pickSound;

    // Token: 0x0400012B RID: 299
    public int count = 1;

    // Token: 0x0400012C RID: 300
    public bool isFinalMushroom;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000034 RID: 52
public class ItemCollector : MonoBehaviour
{
    // Token: 0x06000112 RID: 274 RVA: 0x00005A86 File Offset: 0x00003C86
    private void Start()
    {
        this.LoadMushrooms();
    }

    // Token: 0x06000113 RID: 275 RVA: 0x00005A90 File Offset: 0x00003C90
    private void Update()
    {
        RaycastHit raycastHit;
        if (Input.GetKeyDown(KeyCode.E) && Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out raycastHit, this.pickupRange, this.itemMask))
        {
            Item item;
            if (raycastHit.collider.TryGetComponent<Item>(out item))
            {
                this.items += item.count;
                this.itemText.text = (this.items.ToString() ?? "");
                raycastHit.collider.GetComponent<Item>().OnCollect();
                Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                Action action = this.hasCollected;
                if (action == null)
                {
                    return;
                }
                action();
                return;
            }
            else
            {
                if (raycastHit.collider.CompareTag("Flower"))
                {
                    base.GetComponent<PlayerController>().moveSpeed = 8f;
                    Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                    Object.Destroy(raycastHit.collider.gameObject);
                    return;
                }
                if (raycastHit.collider.CompareTag("Wood"))
                {
                    Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                    this.itemText.transform.parent.gameObject.SetActive(true);
                    this.items++;
                    this.itemText.text = (this.items.ToString() ?? "");
                    Object.Destroy(raycastHit.collider.gameObject);
                    return;
                }
                GunPickup gunPickup;
                if (raycastHit.collider.TryGetComponent<GunPickup>(out gunPickup))
                {
                    Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                    gunPickup.PickupGun();
                    return;
                }
                MemoryGameTrigger memoryGameTrigger;
                if (raycastHit.collider.TryGetComponent<MemoryGameTrigger>(out memoryGameTrigger))
                {
                    memoryGameTrigger.ActivateGame();
                    return;
                }
                if (raycastHit.collider.GetComponent<EndGame>() != null)
                {
                    Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                    raycastHit.collider.GetComponent<EndGame>().FadeToTitles();
                }
            }
        }
    }

    // Token: 0x06000114 RID: 276 RVA: 0x00005CBA File Offset: 0x00003EBA
    public void SaveMushrooms()
    {
        PlayerPrefs.SetInt("mushrooms", this.items);
    }

    // Token: 0x06000115 RID: 277 RVA: 0x00005CCC File Offset: 0x00003ECC
    public void LoadMushrooms()
    {
        this.items = PlayerPrefs.GetInt("mushrooms", 0);
        this.itemText.text = (this.items.ToString() ?? "");
        Action action = this.hasCollected;
        if (action == null)
        {
            return;
        }
        action();
    }

    // Token: 0x0400012D RID: 301
    public Action hasCollected;

    // Token: 0x0400012E RID: 302
    public TextMeshProUGUI itemText;

    // Token: 0x0400012F RID: 303
    public LayerMask itemMask;

    // Token: 0x04000130 RID: 304
    public GameObject pickSound;

    // Token: 0x04000131 RID: 305
    public int items;

    // Token: 0x04000132 RID: 306
    public float pickupRange;
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Token: 0x0200000F RID: 15
public class Joystick : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IDragHandler, IPointerUpHandler
{
    // Token: 0x17000001 RID: 1
    // (get) Token: 0x06000033 RID: 51 RVA: 0x00002903 File Offset: 0x00000B03
    public float Horizontal
    {
        get
        {
            if (!this.snapX)
            {
                return this.input.x;
            }
            return this.SnapFloat(this.input.x, AxisOptions.Horizontal);
        }
    }

    // Token: 0x17000002 RID: 2
    // (get) Token: 0x06000034 RID: 52 RVA: 0x0000292B File Offset: 0x00000B2B
    public float Vertical
    {
        get
        {
            if (!this.snapY)
            {
                return this.input.y;
            }
            return this.SnapFloat(this.input.y, AxisOptions.Vertical);
        }
    }

    // Token: 0x17000003 RID: 3
    // (get) Token: 0x06000035 RID: 53 RVA: 0x00002953 File Offset: 0x00000B53
    public Vector2 Direction
    {
        get
        {
            return new Vector2(this.Horizontal, this.Vertical);
        }
    }

    // Token: 0x17000004 RID: 4
    // (get) Token: 0x06000036 RID: 54 RVA: 0x00002966 File Offset: 0x00000B66
    // (set) Token: 0x06000037 RID: 55 RVA: 0x0000296E File Offset: 0x00000B6E
    public float HandleRange
    {
        get
        {
            return this.handleRange;
        }
        set
        {
            this.handleRange = Mathf.Abs(value);
        }
    }

    // Token: 0x17000005 RID: 5
    // (get) Token: 0x06000038 RID: 56 RVA: 0x0000297C File Offset: 0x00000B7C
    // (set) Token: 0x06000039 RID: 57 RVA: 0x00002984 File Offset: 0x00000B84
    public float DeadZone
    {
        get
        {
            return this.deadZone;
        }
        set
        {
            this.deadZone = Mathf.Abs(value);
        }
    }

    // Token: 0x17000006 RID: 6
    // (get) Token: 0x0600003A RID: 58 RVA: 0x00002992 File Offset: 0x00000B92
    // (set) Token: 0x0600003B RID: 59 RVA: 0x0000299A File Offset: 0x00000B9A
    public AxisOptions AxisOptions
    {
        get
        {
            return this.AxisOptions;
        }
        set
        {
            this.axisOptions = value;
        }
    }

    // Token: 0x17000007 RID: 7
    // (get) Token: 0x0600003C RID: 60 RVA: 0x000029A3 File Offset: 0x00000BA3
    // (set) Token: 0x0600003D RID: 61 RVA: 0x000029AB File Offset: 0x00000BAB
    public bool SnapX
    {
        get
        {
            return this.snapX;
        }
        set
        {
            this.snapX = value;
        }
    }

    // Token: 0x17000008 RID: 8
    // (get) Token: 0x0600003E RID: 62 RVA: 0x000029B4 File Offset: 0x00000BB4
    // (set) Token: 0x0600003F RID: 63 RVA: 0x000029BC File Offset: 0x00000BBC
    public bool SnapY
    {
        get
        {
            return this.snapY;
        }
        set
        {
            this.snapY = value;
        }
    }

    // Token: 0x06000040 RID: 64 RVA: 0x000029C8 File Offset: 0x00000BC8
    protected virtual void Start()
    {
        this.HandleRange = this.handleRange;
        this.DeadZone = this.deadZone;
        this.baseRect = base.GetComponent<RectTransform>();
        this.canvas = base.GetComponentInParent<Canvas>();
        if (this.canvas == null)
        {
            Debug.LogError("The Joystick is not placed inside a canvas");
        }
        Vector2 vector = new Vector2(0.5f, 0.5f);
        this.background.pivot = vector;
        this.handle.anchorMin = vector;
        this.handle.anchorMax = vector;
        this.handle.pivot = vector;
        this.handle.anchoredPosition = Vector2.zero;
    }

    // Token: 0x06000041 RID: 65 RVA: 0x00002A6E File Offset: 0x00000C6E
    public virtual void OnPointerDown(PointerEventData eventData)
    {
        this.OnDrag(eventData);
    }

    // Token: 0x06000042 RID: 66 RVA: 0x00002A78 File Offset: 0x00000C78
    public void OnDrag(PointerEventData eventData)
    {
        this.cam = null;
        if (this.canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            this.cam = this.canvas.worldCamera;
        }
        Vector2 b = RectTransformUtility.WorldToScreenPoint(this.cam, this.background.position);
        Vector2 vector = this.background.sizeDelta / 2f;
        this.input = (eventData.position - b) / (vector * this.canvas.scaleFactor);
        this.FormatInput();
        this.HandleInput(this.input.magnitude, this.input.normalized, vector, this.cam);
        this.handle.anchoredPosition = this.input * vector * this.handleRange;
    }

    // Token: 0x06000043 RID: 67 RVA: 0x00002B4B File Offset: 0x00000D4B
    protected virtual void HandleInput(float magnitude, Vector2 normalised, Vector2 radius, Camera cam)
    {
        if (magnitude > this.deadZone)
        {
            if (magnitude > 1f)
            {
                this.input = normalised;
                return;
            }
        }
        else
        {
            this.input = Vector2.zero;
        }
    }

    // Token: 0x06000044 RID: 68 RVA: 0x00002B74 File Offset: 0x00000D74
    private void FormatInput()
    {
        if (this.axisOptions == AxisOptions.Horizontal)
        {
            this.input = new Vector2(this.input.x, 0f);
            return;
        }
        if (this.axisOptions == AxisOptions.Vertical)
        {
            this.input = new Vector2(0f, this.input.y);
        }
    }

    // Token: 0x06000045 RID: 69 RVA: 0x00002BCC File Offset: 0x00000DCC
    private float SnapFloat(float value, AxisOptions snapAxis)
    {
        if (value == 0f)
        {
            return value;
        }
        if (this.axisOptions == AxisOptions.Both)
        {
            float num = Vector2.Angle(this.input, Vector2.up);
            if (snapAxis == AxisOptions.Horizontal)
            {
                if (num < 22.5f || num > 157.5f)
                {
                    return 0f;
                }
                return (float)((value > 0f) ? 1 : -1);
            }
            else
            {
                if (snapAxis != AxisOptions.Vertical)
                {
                    return value;
                }
                if (num > 67.5f && num < 112.5f)
                {
                    return 0f;
                }
                return (float)((value > 0f) ? 1 : -1);
            }
        }
        else
        {
            if (value > 0f)
            {
                return 1f;
            }
            if (value < 0f)
            {
                return -1f;
            }
            return 0f;
        }
    }

    // Token: 0x06000046 RID: 70 RVA: 0x00002C6F File Offset: 0x00000E6F
    public virtual void OnPointerUp(PointerEventData eventData)
    {
        this.input = Vector2.zero;
        this.handle.anchoredPosition = Vector2.zero;
    }

    // Token: 0x06000047 RID: 71 RVA: 0x00002C8C File Offset: 0x00000E8C
    protected Vector2 ScreenPointToAnchoredPosition(Vector2 screenPosition)
    {
        Vector2 zero = Vector2.zero;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(this.baseRect, screenPosition, this.cam, out zero))
        {
            Vector2 b = this.baseRect.pivot * this.baseRect.sizeDelta;
            return zero - this.background.anchorMax * this.baseRect.sizeDelta + b;
        }
        return Vector2.zero;
    }

    // Token: 0x0400002D RID: 45
    [SerializeField]
    private float handleRange = 1f;

    // Token: 0x0400002E RID: 46
    [SerializeField]
    private float deadZone;

    // Token: 0x0400002F RID: 47
    [SerializeField]
    private AxisOptions axisOptions;

    // Token: 0x04000030 RID: 48
    [SerializeField]
    private bool snapX;

    // Token: 0x04000031 RID: 49
    [SerializeField]
    private bool snapY;

    // Token: 0x04000032 RID: 50
    [SerializeField]
    protected RectTransform background;

    // Token: 0x04000033 RID: 51
    [SerializeField]
    private RectTransform handle;

    // Token: 0x04000034 RID: 52
    private RectTransform baseRect;

    // Token: 0x04000035 RID: 53
    private Canvas canvas;

    // Token: 0x04000036 RID: 54
    private Camera cam;

    // Token: 0x04000037 RID: 55
    private Vector2 input = Vector2.zero;
}

using System;
using UnityEngine;

// Token: 0x0200000D RID: 13
public class JoystickPlayerExample : MonoBehaviour
{
    // Token: 0x0600002B RID: 43 RVA: 0x00002780 File Offset: 0x00000980
    public void FixedUpdate()
    {
        Vector3 a = Vector3.forward * this.variableJoystick.Vertical + Vector3.right * this.variableJoystick.Horizontal;
        this.rb.AddForce(a * this.speed * Time.fixedDeltaTime, ForceMode.VelocityChange);
    }

    // Token: 0x04000026 RID: 38
    public float speed;

    // Token: 0x04000027 RID: 39
    public VariableJoystick variableJoystick;

    // Token: 0x04000028 RID: 40
    public Rigidbody rb;
}

using System;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x0200000E RID: 14
public class JoystickSetterExample : MonoBehaviour
{
    // Token: 0x0600002D RID: 45 RVA: 0x000027E7 File Offset: 0x000009E7
    public void ModeChanged(int index)
    {
        switch (index)
        {
            case 0:
                this.variableJoystick.SetMode(JoystickType.Fixed);
                return;
            case 1:
                this.variableJoystick.SetMode(JoystickType.Floating);
                return;
            case 2:
                this.variableJoystick.SetMode(JoystickType.Dynamic);
                return;
            default:
                return;
        }
    }

    // Token: 0x0600002E RID: 46 RVA: 0x00002824 File Offset: 0x00000A24
    public void AxisChanged(int index)
    {
        switch (index)
        {
            case 0:
                this.variableJoystick.AxisOptions = AxisOptions.Both;
                this.background.sprite = this.axisSprites[index];
                return;
            case 1:
                this.variableJoystick.AxisOptions = AxisOptions.Horizontal;
                this.background.sprite = this.axisSprites[index];
                return;
            case 2:
                this.variableJoystick.AxisOptions = AxisOptions.Vertical;
                this.background.sprite = this.axisSprites[index];
                return;
            default:
                return;
        }
    }

    // Token: 0x0600002F RID: 47 RVA: 0x000028A3 File Offset: 0x00000AA3
    public void SnapX(bool value)
    {
        this.variableJoystick.SnapX = value;
    }

    // Token: 0x06000030 RID: 48 RVA: 0x000028B1 File Offset: 0x00000AB1
    public void SnapY(bool value)
    {
        this.variableJoystick.SnapY = value;
    }

    // Token: 0x06000031 RID: 49 RVA: 0x000028C0 File Offset: 0x00000AC0
    private void Update()
    {
        this.valueText.text = "Current Value: " + this.variableJoystick.Direction.ToString();
    }

    // Token: 0x04000029 RID: 41
    public VariableJoystick variableJoystick;

    // Token: 0x0400002A RID: 42
    public Text valueText;

    // Token: 0x0400002B RID: 43
    public Image background;

    // Token: 0x0400002C RID: 44
    public Sprite[] axisSprites;
}

using System;

// Token: 0x02000015 RID: 21
public enum JoystickType
{
    // Token: 0x04000041 RID: 65
    Fixed,
    // Token: 0x04000042 RID: 66
    Floating,
    // Token: 0x04000043 RID: 67
    Dynamic
}

using System;
using UnityEngine;

// Token: 0x02000032 RID: 50
public class LevelSwitcher : MonoBehaviour
{
    // Token: 0x06000109 RID: 265 RVA: 0x0000598E File Offset: 0x00003B8E
    private void Start()
    {
    }

    // Token: 0x0600010A RID: 266 RVA: 0x00005990 File Offset: 0x00003B90
    private void Update()
    {
    }
}

using System;
using UnityEngine;

// Token: 0x02000050 RID: 80
public class LightSwitcher : MonoBehaviour
{
    // Token: 0x060001A3 RID: 419 RVA: 0x0000832D File Offset: 0x0000652D
    private void Update()
    {
        if (Input.GetAxis("Mouse ScrollWheel") != 0f || Input.GetKeyDown(KeyCode.Q))
        {
            this.SwitchLight();
        }
    }

    // Token: 0x060001A4 RID: 420 RVA: 0x00008350 File Offset: 0x00006550
    private void SwitchLight()
    {
        if (!this.flashlight.activeInHierarchy)
        {
            if (this.oilLamp != null)
            {
                this.oilLamp.SetActive(false);
            }
            this.flashlight.SetActive(true);
            return;
        }
        if (this.flashlight.activeInHierarchy)
        {
            this.flashlight.SetActive(false);
            if (this.oilLamp != null)
            {
                this.oilLamp.SetActive(true);
            }
        }
    }

    // Token: 0x04000204 RID: 516
    public GameObject flashlight;

    // Token: 0x04000205 RID: 517
    public GameObject oilLamp;
}

using System;

// Token: 0x02000077 RID: 119
public enum LocalizationEnum
{
    // Token: 0x040002C1 RID: 705
    English,
    // Token: 0x040002C2 RID: 706
    Polish
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x0200003E RID: 62
public class LockUI : MonoBehaviour
{
    // Token: 0x0600014B RID: 331 RVA: 0x00006A89 File Offset: 0x00004C89
    private void Awake()
    {
        this.LoadPassword();
    }

    // Token: 0x0600014C RID: 332 RVA: 0x00006A91 File Offset: 0x00004C91
    public void SwitchPanelState(bool state)
    {
        this.lockPanel.SetActive(state);
    }

    // Token: 0x0600014D RID: 333 RVA: 0x00006AA0 File Offset: 0x00004CA0
    public void CheckPassword()
    {
        if (this.inputField.text.ToLower() == this.password)
        {
            this.checker.enabled = true;
            this.hintText.SetActive(false);
            this.inputField.gameObject.SetActive(false);
            this.rewardMessage.SetActive(true);
            base.gameObject.SetActive(false);
            this.enteredPassword = this.password;
            this.SavePassword();
        }
    }

    // Token: 0x0600014E RID: 334 RVA: 0x00006B1D File Offset: 0x00004D1D
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.lockPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
        }
    }

    // Token: 0x0600014F RID: 335 RVA: 0x00006B4E File Offset: 0x00004D4E
    public void OnFoodKilled()
    {
        this.foodKilled = "true";
        this.SavePassword();
        this.Activate();
    }

    // Token: 0x06000150 RID: 336 RVA: 0x00006B68 File Offset: 0x00004D68
    private void LoadPassword()
    {
        this.enteredPassword = PlayerPrefs.GetString("Password", "");
        this.foodKilled = PlayerPrefs.GetString("FoodKilled", "");
        if (this.enteredPassword == "offscreen")
        {
            this.checker.enabled = true;
            base.gameObject.SetActive(false);
            return;
        }
        if (this.foodKilled == "true")
        {
            this.Activate();
        }
    }

    // Token: 0x06000151 RID: 337 RVA: 0x00006BE2 File Offset: 0x00004DE2
    private void SavePassword()
    {
        PlayerPrefs.SetString("Password", this.enteredPassword);
        PlayerPrefs.SetString("FoodKilled", this.foodKilled);
    }

    // Token: 0x06000152 RID: 338 RVA: 0x00006C04 File Offset: 0x00004E04
    public void Activate()
    {
        base.GetComponent<Outline>().enabled = true;
        base.GetComponent<SphereCollider>().enabled = true;
    }

    // Token: 0x0400018F RID: 399
    [SerializeField]
    private GameObject lockPanel;

    // Token: 0x04000190 RID: 400
    [SerializeField]
    private GameObject rewardMessage;

    // Token: 0x04000191 RID: 401
    [SerializeField]
    private GameObject hintText;

    // Token: 0x04000192 RID: 402
    [SerializeField]
    private TMP_InputField inputField;

    // Token: 0x04000193 RID: 403
    [SerializeField]
    private MushroomChecker checker;

    // Token: 0x04000194 RID: 404
    [SerializeField]
    private string password;

    // Token: 0x04000195 RID: 405
    private string foodKilled = "";

    // Token: 0x04000196 RID: 406
    private string enteredPassword = "";
}

using System;
using UnityEngine;

// Token: 0x02000016 RID: 22
public class Melon : MonoBehaviour
{
    // Token: 0x0600005D RID: 93 RVA: 0x00002FC4 File Offset: 0x000011C4
    private void Start()
    {
        this._rigidbody = base.GetComponent<Rigidbody>();
    }

    // Token: 0x0600005E RID: 94 RVA: 0x00002FD2 File Offset: 0x000011D2
    private void FixedUpdate()
    {
        this._rigidbody.AddForce(base.transform.forward * this.moveSpeed, ForceMode.Force);
    }

    // Token: 0x0600005F RID: 95 RVA: 0x00002FF6 File Offset: 0x000011F6
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            this.DestroyMelon();
        }
    }

    // Token: 0x06000060 RID: 96 RVA: 0x0000301A File Offset: 0x0000121A
    public void DestroyMelon()
    {
        Object.Instantiate<GameObject>(this.destroyEffect, base.transform.position, Quaternion.identity);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x06000061 RID: 97 RVA: 0x00003044 File Offset: 0x00001244
    private void OnCollisionEnter(Collision other)
    {
        PlayerHealth playerHealth;
        if (other.gameObject.CompareTag("Player") && other.gameObject.TryGetComponent<PlayerHealth>(out playerHealth))
        {
            playerHealth.TakeDamage(this.damage);
            this.DestroyMelon();
        }
    }

    // Token: 0x04000044 RID: 68
    public float moveSpeed;

    // Token: 0x04000045 RID: 69
    public float health;

    // Token: 0x04000046 RID: 70
    public float damage;

    // Token: 0x04000047 RID: 71
    public GameObject destroyEffect;

    // Token: 0x04000048 RID: 72
    private Rigidbody _rigidbody;
}

using System;
using UnityEngine;

// Token: 0x02000017 RID: 23
public class MelonExitTrigger : MonoBehaviour
{
    // Token: 0x06000063 RID: 99 RVA: 0x0000308C File Offset: 0x0000128C
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.hitboxes.SetActive(false);
            this.cylinder.SetActive(true);
        }
    }

    // Token: 0x04000049 RID: 73
    public GameObject hitboxes;

    // Token: 0x0400004A RID: 74
    public GameObject cylinder;
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Token: 0x02000018 RID: 24
public class MelonfallManager : MonoBehaviour
{
    // Token: 0x1700000B RID: 11
    // (get) Token: 0x06000065 RID: 101 RVA: 0x000030BB File Offset: 0x000012BB
    private float startSpawnTime
    {
        get
        {
            return this.decreaseCurve.Evaluate(this._time);
        }
    }

    // Token: 0x06000066 RID: 102 RVA: 0x000030CE File Offset: 0x000012CE
    private void Start()
    {
        this._animator = base.GetComponent<Animator>();
    }

    // Token: 0x06000067 RID: 103 RVA: 0x000030DC File Offset: 0x000012DC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.StartGame();
            base.GetComponent<Collider>().enabled = false;
        }
    }

    // Token: 0x06000068 RID: 104 RVA: 0x000030FD File Offset: 0x000012FD
    public void StartGame()
    {
        this._isInGame = true;
        this.timerText.gameObject.SetActive(true);
        MusicManager.instance.EnableMusic(2);
        this._animator.SetTrigger("hasEntered");
    }

    // Token: 0x06000069 RID: 105 RVA: 0x00003134 File Offset: 0x00001334
    private void Update()
    {
        if (this._isInGame)
        {
            this.showTime = this.startSpawnTime;
            this._spawnTime -= Time.deltaTime;
            this._time += Time.deltaTime;
            float num = (float)Mathf.FloorToInt((60f - this._time) / 60f);
            float num2 = (float)Mathf.FloorToInt((60f - this._time) % 60f);
            this.timerText.text = string.Format("{0:00}:{1:00}", num, num2);
            if (this._spawnTime <= 0f)
            {
                if (this.startSpawnTime < 1.3f)
                {
                    this.SpawnMelons(3);
                }
                else if (this.startSpawnTime < 2f)
                {
                    this.SpawnMelons(2);
                }
                else
                {
                    this.SpawnMelons(1);
                }
                this._spawnTime = this.startSpawnTime;
            }
            if (this._time >= this.totalGameTime)
            {
                this._isInGame = false;
                this.timerText.gameObject.SetActive(false);
                Melon[] array = Object.FindObjectsOfType<Melon>();
                for (int i = 0; i < array.Length; i++)
                {
                    array[i].DestroyMelon();
                }
                Object.Instantiate<GameObject>(this.melonfallOverSound, base.transform.position, Quaternion.identity);
                this._animator.SetBool("isCompleted", true);
                Action action = this.hasCompletedMelonfall;
                if (action == null)
                {
                    return;
                }
                action();
            }
        }
    }

    // Token: 0x0600006A RID: 106 RVA: 0x0000329C File Offset: 0x0000149C
    public void SpawnMelons(int numberOfMelons)
    {
        if (numberOfMelons > this.spawnPositions.Length)
        {
            Debug.LogWarning("Number of melons to spawn exceeds available spawn positions.");
            numberOfMelons = this.spawnPositions.Length;
        }
        List<int> list = new List<int>();
        for (int i = 0; i < this.spawnPositions.Length; i++)
        {
            list.Add(i);
        }
        this.ShuffleList(list);
        for (int j = 0; j < numberOfMelons; j++)
        {
            int num = list[j];
            Melon component;
            if (numberOfMelons >= 3 && j == 0)
            {
                component = Object.Instantiate<GameObject>(this.melonShootable, this.spawnPositions[num].position, this.spawnPositions[num].rotation).GetComponent<Melon>();
            }
            else
            {
                component = Object.Instantiate<GameObject>(this.melon, this.spawnPositions[num].position, this.spawnPositions[num].rotation).GetComponent<Melon>();
            }
            component.moveSpeed = 1f / (this.startSpawnTime / 1.3f);
        }
    }

    // Token: 0x0600006B RID: 107 RVA: 0x00003388 File Offset: 0x00001588
    private void ShuffleList(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int index = Random.Range(i, list.Count);
            int value = list[i];
            list[i] = list[index];
            list[index] = value;
        }
    }

    // Token: 0x0400004B RID: 75
    [SerializeField]
    private Transform[] spawnPositions;

    // Token: 0x0400004C RID: 76
    [SerializeField]
    private GameObject melon;

    // Token: 0x0400004D RID: 77
    [SerializeField]
    private GameObject melonShootable;

    // Token: 0x0400004E RID: 78
    [SerializeField]
    private GameObject melonfallOverSound;

    // Token: 0x0400004F RID: 79
    [SerializeField]
    private AnimationCurve decreaseCurve;

    // Token: 0x04000050 RID: 80
    [SerializeField]
    private TextMeshProUGUI timerText;

    // Token: 0x04000051 RID: 81
    private Animator _animator;

    // Token: 0x04000052 RID: 82
    public Action hasCompletedMelonfall;

    // Token: 0x04000053 RID: 83
    public float totalGameTime;

    // Token: 0x04000054 RID: 84
    public float showTime;

    // Token: 0x04000055 RID: 85
    private float _time;

    // Token: 0x04000056 RID: 86
    private float _spawnTime;

    // Token: 0x04000057 RID: 87
    private bool _isInGame;
}

using System;
using UnityEngine;

// Token: 0x02000019 RID: 25
public class MemoryGameTrigger : MonoBehaviour
{
    // Token: 0x0600006D RID: 109 RVA: 0x000033D9 File Offset: 0x000015D9
    private void Start()
    {
        this.spriteRenderer = base.GetComponent<SpriteRenderer>();
    }

    // Token: 0x0600006E RID: 110 RVA: 0x000033E7 File Offset: 0x000015E7
    public void ActivateGame()
    {
        this.spriteRenderer.sprite = this.inGameSprite;
        this.memoryGame.SetActive(true);
    }

    // Token: 0x0600006F RID: 111 RVA: 0x00003406 File Offset: 0x00001606
    public void CloseGame()
    {
        this.spriteRenderer.sprite = this.interruptGameSprite;
        this.memoryGame.SetActive(false);
    }

    // Token: 0x06000070 RID: 112 RVA: 0x00003428 File Offset: 0x00001628
    public void OnGameOver()
    {
        base.GetComponent<Collider>().enabled = false;
        this.memoryGame.SetActive(false);
        this.spriteRenderer.sprite = this.gameOverSprite;
        this.thingsToTeleport.SetActive(true);
        this.player.transform.parent = this.thingsToTeleport.transform;
        this.thingsToTeleport.transform.position = this.teleportDestinationPoint.position;
        this.thingsToTeleport.transform.rotation = this.teleportDestinationPoint.rotation;
        this.player.transform.parent = null;
        this.player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        this.playerCamera.FlipPlayer();
    }

    // Token: 0x04000058 RID: 88
    [SerializeField]
    private GameObject memoryGame;

    // Token: 0x04000059 RID: 89
    [SerializeField]
    private GameObject thingsToTeleport;

    // Token: 0x0400005A RID: 90
    [SerializeField]
    private Transform player;

    // Token: 0x0400005B RID: 91
    [SerializeField]
    private PlayerCamera playerCamera;

    // Token: 0x0400005C RID: 92
    [SerializeField]
    private Transform teleportDestinationPoint;

    // Token: 0x0400005D RID: 93
    [SerializeField]
    private Sprite interruptGameSprite;

    // Token: 0x0400005E RID: 94
    [SerializeField]
    private Sprite inGameSprite;

    // Token: 0x0400005F RID: 95
    [SerializeField]
    private Sprite gameOverSprite;

    // Token: 0x04000060 RID: 96
    private SpriteRenderer spriteRenderer;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Token: 0x0200005F RID: 95
public class Menu : MonoBehaviour
{
    // Token: 0x060001E7 RID: 487 RVA: 0x00009225 File Offset: 0x00007425
    private void Start()
    {
        this.sceneIndex = PlayerPrefs.GetInt("Scene", 1);
    }

    // Token: 0x060001E8 RID: 488 RVA: 0x00009238 File Offset: 0x00007438
    public void LoadGame()
    {
        this.sceneIndex = PlayerPrefs.GetInt("Scene", 1);
        base.StartCoroutine(this.LoadScene_Coroutine(this.sceneIndex));
    }

    // Token: 0x060001E9 RID: 489 RVA: 0x0000925E File Offset: 0x0000745E
    public void LoadNewGame()
    {
        PlayerPrefs.DeleteAll();
        this.LoadGame();
    }

    // Token: 0x060001EA RID: 490 RVA: 0x0000926B File Offset: 0x0000746B
    public IEnumerator LoadScene_Coroutine(int index)
    {
        this.progressSlider.value = 0f;
        this.LoaderUI.SetActive(true);
        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(index);
        asyncOperation.allowSceneActivation = false;
        float progress = 0f;
        while (!asyncOperation.isDone)
        {
            progress = Mathf.MoveTowards(progress, asyncOperation.progress, Time.deltaTime);
            this.progressSlider.value = progress;
            if (progress >= 0.9f)
            {
                this.progressSlider.value = 1f;
                asyncOperation.allowSceneActivation = true;
            }
            yield return null;
        }
        yield break;
    }

    // Token: 0x0400025A RID: 602
    public int sceneIndex;

    // Token: 0x0400025B RID: 603
    public GameObject LoaderUI;

    // Token: 0x0400025C RID: 604
    public Slider progressSlider;
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Token: 0x02000060 RID: 96
public class MenuExit : MonoBehaviour
{
    // Token: 0x060001EC RID: 492 RVA: 0x0000928C File Offset: 0x0000748C
    public void OnExit()
    {
        Time.timeScale = 1f;
        MusicManager musicManager = Object.FindObjectOfType<MusicManager>();
        if (musicManager.audioSource.clip != musicManager.musicVariants[0])
        {
            musicManager.EnableMusic(0);
        }
        this.SaveItemPresence();
        SceneManager.LoadScene("Menu");
    }

    // Token: 0x060001ED RID: 493 RVA: 0x000092DC File Offset: 0x000074DC
    public void SaveItemPresence()
    {
        Item[] array = this.items;
        for (int i = 0; i < array.Length; i++)
        {
            array[i].SavePresence();
        }
    }

    // Token: 0x0400025D RID: 605
    public Item[] items;
}

using System;
using UnityEngine;

// Token: 0x0200001A RID: 26
public class MoonGravityTrigger : MonoBehaviour
{
    // Token: 0x06000072 RID: 114 RVA: 0x00003503 File Offset: 0x00001703
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !this._hasTriggered)
        {
            this._hasTriggered = true;
            if (this.isMoon)
            {
                GravityController.instance.SetMoonGravity();
                return;
            }
            GravityController.instance.SetEarthGravity();
        }
    }

    // Token: 0x04000061 RID: 97
    public bool isMoon;

    // Token: 0x04000062 RID: 98
    private bool _hasTriggered;
}

using System;
using UnityEngine;

// Token: 0x0200007B RID: 123
public class MouseLook : MonoBehaviour
{
    // Token: 0x06000245 RID: 581 RVA: 0x0000AEC0 File Offset: 0x000090C0
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Token: 0x06000246 RID: 582 RVA: 0x0000AEC8 File Offset: 0x000090C8
    private void Update()
    {
        float d = Input.GetAxis("Mouse X") * this.mouseSensitivity * Time.deltaTime;
        float num = Input.GetAxis("Mouse Y") * this.mouseSensitivity * Time.deltaTime;
        this.xRotation -= num;
        this.xRotation = Mathf.Clamp(this.xRotation, -90f, 90f);
        base.transform.localRotation = Quaternion.Euler(this.xRotation, 0f, 0f);
        this.playerBody.Rotate(Vector3.up * d);
    }

    // Token: 0x040002CE RID: 718
    public float mouseSensitivity = 100f;

    // Token: 0x040002CF RID: 719
    public Transform playerBody;

    // Token: 0x040002D0 RID: 720
    private float xRotation;
}

using System;
using UnityEngine;

// Token: 0x02000051 RID: 81
public class MoveCamera : MonoBehaviour
{
    // Token: 0x060001A6 RID: 422 RVA: 0x000083CC File Offset: 0x000065CC
    private void FixedUpdate()
    {
        if (this.currentMode == MoveCamera.UpdateMode.FixedUpdate)
        {
            this.ApplyMovement();
        }
    }

    // Token: 0x060001A7 RID: 423 RVA: 0x000083DD File Offset: 0x000065DD
    private void Update()
    {
        if (this.currentMode == MoveCamera.UpdateMode.Update)
        {
            this.ApplyMovement();
        }
    }

    // Token: 0x060001A8 RID: 424 RVA: 0x000083EE File Offset: 0x000065EE
    private void LateUpdate()
    {
        if (this.currentMode == MoveCamera.UpdateMode.LateUpdate)
        {
            this.ApplyMovement();
        }
    }

    // Token: 0x060001A9 RID: 425 RVA: 0x000083FE File Offset: 0x000065FE
    private void ApplyMovement()
    {
        base.transform.position = this.cameraPosition.position;
    }

    // Token: 0x04000206 RID: 518
    public Transform cameraPosition;

    // Token: 0x04000207 RID: 519
    public MoveCamera.UpdateMode currentMode;

    // Token: 0x020000D0 RID: 208
    public enum UpdateMode
    {
        // Token: 0x0400047B RID: 1147
        LateUpdate,
        // Token: 0x0400047C RID: 1148
        Update,
        // Token: 0x0400047D RID: 1149
        FixedUpdate
    }
}

using System;
using UnityEngine;

// Token: 0x02000052 RID: 82
public class MoveForward : MonoBehaviour
{
    // Token: 0x060001AB RID: 427 RVA: 0x0000841E File Offset: 0x0000661E
    private void Update()
    {
        base.transform.Translate(Vector3.forward * this.speed * Time.deltaTime);
    }

    // Token: 0x04000208 RID: 520
    public float speed;
}

using System;
using UnityEngine;

// Token: 0x02000035 RID: 53
public class MushroomChecker : MonoBehaviour
{
    // Token: 0x06000117 RID: 279 RVA: 0x00005D21 File Offset: 0x00003F21
    private void Start()
    {
        this.collector = Object.FindObjectOfType<ItemCollector>();
    }

    // Token: 0x06000118 RID: 280 RVA: 0x00005D30 File Offset: 0x00003F30
    private void Update()
    {
        this.playerNearGate = Physics.CheckSphere(this.openingPoint.position, this.sightRange, this.whatIsPlayer);
        this.playerPastGate = Physics.CheckSphere(this.closingPoint.position, this.sightRange, this.whatIsPlayer);
        if (this.playerNearGate)
        {
            this.CheckForMushrooms();
        }
        if (this.playerPastGate)
        {
            this.CloseGate();
        }
    }

    // Token: 0x06000119 RID: 281 RVA: 0x00005DA8 File Offset: 0x00003FA8
    public void CheckForMushrooms()
    {
        if (this.collector.items >= this.mushroomsRequired && this.sable.activeInHierarchy)
        {
            this.gateAnimator.SetBool("hasMushrooms", true);
        }
        if (!this.alreadySaved)
        {
            this.alreadySaved = true;
            this.collector.SaveMushrooms();
            this.collector.GetComponent<PlayerController>().SavePosition();
            Object.FindObjectOfType<MenuExit>(true).SaveItemPresence();
        }
    }

    // Token: 0x0600011A RID: 282 RVA: 0x00005E1B File Offset: 0x0000401B
    public void CloseGate()
    {
        this.gateAnimator.SetBool("pastGate", true);
        base.enabled = false;
    }

    // Token: 0x0600011B RID: 283 RVA: 0x00005E38 File Offset: 0x00004038
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(this.openingPoint.position, this.sightRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(this.closingPoint.position, this.sightRange);
    }

    // Token: 0x04000133 RID: 307
    public GameObject sable;

    // Token: 0x04000134 RID: 308
    public Transform openingPoint;

    // Token: 0x04000135 RID: 309
    public Transform closingPoint;

    // Token: 0x04000136 RID: 310
    public Animator gateAnimator;

    // Token: 0x04000137 RID: 311
    public LayerMask whatIsPlayer;

    // Token: 0x04000138 RID: 312
    public float sightRange;

    // Token: 0x04000139 RID: 313
    public int mushroomsRequired;

    // Token: 0x0400013A RID: 314
    private ItemCollector collector;

    // Token: 0x0400013B RID: 315
    private bool playerNearGate;

    // Token: 0x0400013C RID: 316
    private bool playerPastGate;

    // Token: 0x0400013D RID: 317
    private bool alreadySaved;
}

using System;
using UnityEngine;

// Token: 0x0200003F RID: 63
public class MushroomDamage : MonoBehaviour
{
    // Token: 0x06000154 RID: 340 RVA: 0x00006C3C File Offset: 0x00004E3C
    private void Start()
    {
        this.shailushay = Object.FindObjectOfType<Shailushay>();
    }

    // Token: 0x06000155 RID: 341 RVA: 0x00006C49 File Offset: 0x00004E49
    private void OnDestroy()
    {
        if (this.shailushay != null)
        {
            this.shailushay.TakeDamage(this.damage);
        }
    }

    // Token: 0x04000197 RID: 407
    [SerializeField]
    private int damage;

    // Token: 0x04000198 RID: 408
    private Shailushay shailushay;
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Token: 0x02000036 RID: 54
public class MushroomGame : MonoBehaviour
{
    // Token: 0x0600011D RID: 285 RVA: 0x00005E8D File Offset: 0x0000408D
    private void OnTriggerEnter(Collider other)
    {
        if (!this.isReady && other.transform.root.CompareTag("Player"))
        {
            this.StartGame();
        }
    }

    // Token: 0x0600011E RID: 286 RVA: 0x00005EB4 File Offset: 0x000040B4
    private void StartGame()
    {
        this.gameTime = this.startGameTime;
        this.timerText.gameObject.SetActive(true);
        this.isReady = true;
        base.GetComponent<SphereCollider>().enabled = false;
        Object.FindObjectOfType<MusicManager>().EnableMusic(1);
    }

    // Token: 0x0600011F RID: 287 RVA: 0x00005EF4 File Offset: 0x000040F4
    private void Update()
    {
        if (this.isReady)
        {
            if (this.gameTime > 0f)
            {
                float num = (float)Mathf.FloorToInt(this.gameTime / 60f);
                float num2 = (float)Mathf.FloorToInt(this.gameTime % 60f);
                this.timerText.text = string.Format("{0:00}:{1:00}", num, num2);
                this.gameTime -= Time.deltaTime;
                if (this.timeBetweenSpawn > 0f)
                {
                    this.timeBetweenSpawn -= Time.deltaTime;
                    return;
                }
                int num3 = Random.Range(0, this.enemies.Length);
                int num4 = Random.Range(0, this.spawnPoints.Length);
                Object.Instantiate<GameObject>(this.enemies[num3], this.spawnPoints[num4].position, Quaternion.identity);
                this.timeBetweenSpawn = this.startTimeBetweenSpawn;
                if (this.startTimeBetweenSpawn > this.minTime)
                {
                    this.startTimeBetweenSpawn -= this.decreaseTime;
                    return;
                }
            }
            else if (!this.alreadyStarted)
            {
                this.alreadyStarted = true;
                base.StartCoroutine(this.StartBossfight());
            }
        }
    }

    // Token: 0x06000120 RID: 288 RVA: 0x0000601D File Offset: 0x0000421D
    public IEnumerator StartBossfight()
    {
        MushroomHunter[] array = Object.FindObjectsOfType<MushroomHunter>();
        for (int i = 0; i < array.Length; i++)
        {
            array[i].TakeDamage(1000);
        }
        this.timerText.gameObject.SetActive(false);
        this.mushroomAnimator.SetBool("gameOver", true);
        yield return new WaitForSeconds(2f);
        this.player.transform.position = this.posBeforeBossfight.position;
        this.player.transform.rotation = this.posBeforeBossfight.rotation;
        this.player.enabled = false;
        yield return new WaitForSeconds(2f);
        this.fenceAnimator.SetBool("bossfightStarted", true);
        yield return new WaitForSeconds(4f);
        this.bossHP.SetActive(true);
        Object.Instantiate<GameObject>(this.boss, this.bossSpawnPos.position, Quaternion.identity);
        this.player.enabled = true;
        base.enabled = false;
        yield break;
    }

    // Token: 0x0400013E RID: 318
    public GameObject[] enemies;

    // Token: 0x0400013F RID: 319
    public Transform[] spawnPoints;

    // Token: 0x04000140 RID: 320
    public TextMeshProUGUI timerText;

    // Token: 0x04000141 RID: 321
    public PlayerController player;

    // Token: 0x04000142 RID: 322
    public Animator mushroomAnimator;

    // Token: 0x04000143 RID: 323
    public Animator fenceAnimator;

    // Token: 0x04000144 RID: 324
    public GameObject boss;

    // Token: 0x04000145 RID: 325
    public GameObject bossHP;

    // Token: 0x04000146 RID: 326
    public Transform posBeforeBossfight;

    // Token: 0x04000147 RID: 327
    public Transform bossSpawnPos;

    // Token: 0x04000148 RID: 328
    private float gameTime;

    // Token: 0x04000149 RID: 329
    public float startGameTime;

    // Token: 0x0400014A RID: 330
    private float timeBetweenSpawn;

    // Token: 0x0400014B RID: 331
    public float startTimeBetweenSpawn;

    // Token: 0x0400014C RID: 332
    public float decreaseTime;

    // Token: 0x0400014D RID: 333
    public float minTime;

    // Token: 0x0400014E RID: 334
    private bool isReady;

    // Token: 0x0400014F RID: 335
    private bool alreadyStarted;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x02000037 RID: 55
public class MushroomHunter : MonoBehaviour
{
    // Token: 0x06000122 RID: 290 RVA: 0x00006034 File Offset: 0x00004234
    private void Awake()
    {
        this.agent = base.GetComponent<NavMeshAgent>();
    }

    // Token: 0x06000123 RID: 291 RVA: 0x00006042 File Offset: 0x00004242
    private void Start()
    {
        this.walkPoint = base.transform.position;
    }

    // Token: 0x06000124 RID: 292 RVA: 0x00006058 File Offset: 0x00004258
    private void Update()
    {
        this.FindClosestMushroom();
        this.mushroomInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsMushroom);
        this.mushroomInPickRange = Physics.CheckSphere(base.transform.position, this.pickRange, this.whatIsMushroom);
        if (!this.hasPicked)
        {
            if (!this.mushroomInSightRange && !this.mushroomInPickRange)
            {
                this.Patroling();
            }
            if (this.mushroomInSightRange && !this.mushroomInPickRange)
            {
                this.ChaseMushroom();
            }
            if (this.mushroomInPickRange && this.mushroomInSightRange)
            {
                this.PickMushroom();
                return;
            }
        }
        else
        {
            this.Patroling();
        }
    }

    // Token: 0x06000125 RID: 293 RVA: 0x0000610C File Offset: 0x0000430C
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 5f)
        {
            this.walkPointSet = false;
            if (this.hasPicked)
            {
                this.hasPicked = false;
                this.DestroyEnemy();
            }
        }
    }

    // Token: 0x06000126 RID: 294 RVA: 0x00006184 File Offset: 0x00004384
    public void FindClosestMushroom()
    {
        float num = float.PositiveInfinity;
        foreach (GameObject gameObject in GameObject.FindGameObjectsWithTag("CentralMushroom"))
        {
            float sqrMagnitude = (gameObject.transform.position - base.transform.position).sqrMagnitude;
            if (sqrMagnitude < num)
            {
                num = sqrMagnitude;
                this.closestMushroom = gameObject.transform;
            }
        }
    }

    // Token: 0x06000127 RID: 295 RVA: 0x000061EE File Offset: 0x000043EE
    private void SearchWalkPoint()
    {
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x06000128 RID: 296 RVA: 0x00006224 File Offset: 0x00004424
    private void ChaseMushroom()
    {
        this.agent.SetDestination(this.closestMushroom.position);
    }

    // Token: 0x06000129 RID: 297 RVA: 0x00006240 File Offset: 0x00004440
    private void PickMushroom()
    {
        this.hasPicked = true;
        this.closestMushroom.transform.parent = this.mushroomPos;
        this.closestMushroom.transform.position = this.mushroomPos.position;
        this.closestMushroom.transform.rotation = this.mushroomPos.rotation;
    }

    // Token: 0x0600012A RID: 298 RVA: 0x000062A0 File Offset: 0x000044A0
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            this.DestroyEnemy();
        }
    }

    // Token: 0x0600012B RID: 299 RVA: 0x000062C4 File Offset: 0x000044C4
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        if (this.hasPicked)
        {
            Object.Instantiate<GameObject>(this.newMushroom, base.transform.position, Quaternion.identity);
        }
        Object.Destroy(base.gameObject);
    }

    // Token: 0x0600012C RID: 300 RVA: 0x0000631C File Offset: 0x0000451C
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.pickRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x04000150 RID: 336
    public NavMeshAgent agent;

    // Token: 0x04000151 RID: 337
    public Transform mushroomPos;

    // Token: 0x04000152 RID: 338
    public Transform closestMushroom;

    // Token: 0x04000153 RID: 339
    public LayerMask whatIsGround;

    // Token: 0x04000154 RID: 340
    public LayerMask whatIsMushroom;

    // Token: 0x04000155 RID: 341
    public Vector3 walkPoint;

    // Token: 0x04000156 RID: 342
    private bool walkPointSet;

    // Token: 0x04000157 RID: 343
    public float walkPointRange;

    // Token: 0x04000158 RID: 344
    public float sightRange;

    // Token: 0x04000159 RID: 345
    public float pickRange;

    // Token: 0x0400015A RID: 346
    public bool mushroomInSightRange;

    // Token: 0x0400015B RID: 347
    public bool mushroomInPickRange;

    // Token: 0x0400015C RID: 348
    public bool hasPicked;

    // Token: 0x0400015D RID: 349
    public float health;

    // Token: 0x0400015E RID: 350
    public int damage;

    // Token: 0x0400015F RID: 351
    public GameObject deathEffect;

    // Token: 0x04000160 RID: 352
    public GameObject newMushroom;
}

using System;
using UnityEngine;

// Token: 0x0200005A RID: 90
public class MusicManager : MonoBehaviour
{
    // Token: 0x060001CF RID: 463 RVA: 0x00008DD1 File Offset: 0x00006FD1
    private void Awake()
    {
        if (MusicManager.instance != null)
        {
            Object.Destroy(base.gameObject);
            return;
        }
        MusicManager.instance = this;
        Object.DontDestroyOnLoad(base.transform.gameObject);
    }

    // Token: 0x060001D0 RID: 464 RVA: 0x00008E02 File Offset: 0x00007002
    private void Start()
    {
        this.audioSource.clip = this.musicVariants[0];
        this.audioSource.Play();
    }

    // Token: 0x060001D1 RID: 465 RVA: 0x00008E24 File Offset: 0x00007024
    public void EnableMusic(int musicIndex)
    {
        if (this.audioSource.clip != this.musicVariants[musicIndex])
        {
            this.audioSource.clip = this.musicVariants[musicIndex];
            this.audioSource.volume = this.volumes[musicIndex];
            this.audioSource.Play();
        }
    }

    // Token: 0x0400023F RID: 575
    public static MusicManager instance;

    // Token: 0x04000240 RID: 576
    public AudioSource audioSource;

    // Token: 0x04000241 RID: 577
    public AudioClip[] musicVariants;

    // Token: 0x04000242 RID: 578
    [Range(0f, 1f)]
    public float[] volumes;
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Token: 0x0200007D RID: 125
[DisallowMultipleComponent]
public class Outline : MonoBehaviour
{
    // Token: 0x17000011 RID: 17
    // (get) Token: 0x0600024A RID: 586 RVA: 0x0000B104 File Offset: 0x00009304
    // (set) Token: 0x0600024B RID: 587 RVA: 0x0000B10C File Offset: 0x0000930C
    public Outline.Mode OutlineMode
    {
        get
        {
            return this.outlineMode;
        }
        set
        {
            this.outlineMode = value;
            this.needsUpdate = true;
        }
    }

    // Token: 0x17000012 RID: 18
    // (get) Token: 0x0600024C RID: 588 RVA: 0x0000B11C File Offset: 0x0000931C
    // (set) Token: 0x0600024D RID: 589 RVA: 0x0000B124 File Offset: 0x00009324
    public Color OutlineColor
    {
        get
        {
            return this.outlineColor;
        }
        set
        {
            this.outlineColor = value;
            this.needsUpdate = true;
        }
    }

    // Token: 0x17000013 RID: 19
    // (get) Token: 0x0600024E RID: 590 RVA: 0x0000B134 File Offset: 0x00009334
    // (set) Token: 0x0600024F RID: 591 RVA: 0x0000B13C File Offset: 0x0000933C
    public float OutlineWidth
    {
        get
        {
            return this.outlineWidth;
        }
        set
        {
            this.outlineWidth = value;
            this.needsUpdate = true;
        }
    }

    // Token: 0x06000250 RID: 592 RVA: 0x0000B14C File Offset: 0x0000934C
    private void Awake()
    {
        this.renderers = base.GetComponentsInChildren<Renderer>();
        this.outlineMaskMaterial = Object.Instantiate<Material>(Resources.Load<Material>("Materials/OutlineMask"));
        this.outlineFillMaterial = Object.Instantiate<Material>(Resources.Load<Material>("Materials/OutlineFill"));
        this.outlineMaskMaterial.name = "OutlineMask (Instance)";
        this.outlineFillMaterial.name = "OutlineFill (Instance)";
        this.LoadSmoothNormals();
        this.needsUpdate = true;
    }

    // Token: 0x06000251 RID: 593 RVA: 0x0000B1BC File Offset: 0x000093BC
    private void OnEnable()
    {
        foreach (Renderer renderer in this.renderers)
        {
            List<Material> list = renderer.sharedMaterials.ToList<Material>();
            list.Add(this.outlineMaskMaterial);
            list.Add(this.outlineFillMaterial);
            renderer.materials = list.ToArray();
        }
    }

    // Token: 0x06000252 RID: 594 RVA: 0x0000B210 File Offset: 0x00009410
    private void OnMouseOver()
    {
        this.outlineWidth = 10f;
        this.needsUpdate = true;
    }

    // Token: 0x06000253 RID: 595 RVA: 0x0000B224 File Offset: 0x00009424
    private void OnMouseExit()
    {
        this.outlineWidth = 0f;
        this.needsUpdate = true;
    }

    // Token: 0x06000254 RID: 596 RVA: 0x0000B238 File Offset: 0x00009438
    private void OnValidate()
    {
        this.needsUpdate = true;
        if ((!this.precomputeOutline && this.bakeKeys.Count != 0) || this.bakeKeys.Count != this.bakeValues.Count)
        {
            this.bakeKeys.Clear();
            this.bakeValues.Clear();
        }
        if (this.precomputeOutline && this.bakeKeys.Count == 0)
        {
            this.Bake();
        }
    }

    // Token: 0x06000255 RID: 597 RVA: 0x0000B2AA File Offset: 0x000094AA
    private void Update()
    {
        if (this.needsUpdate)
        {
            this.needsUpdate = false;
            this.UpdateMaterialProperties();
        }
    }

    // Token: 0x06000256 RID: 598 RVA: 0x0000B2C4 File Offset: 0x000094C4
    private void OnDisable()
    {
        foreach (Renderer renderer in this.renderers)
        {
            List<Material> list = renderer.sharedMaterials.ToList<Material>();
            list.Remove(this.outlineMaskMaterial);
            list.Remove(this.outlineFillMaterial);
            renderer.materials = list.ToArray();
        }
    }

    // Token: 0x06000257 RID: 599 RVA: 0x0000B31A File Offset: 0x0000951A
    private void OnDestroy()
    {
        Object.Destroy(this.outlineMaskMaterial);
        Object.Destroy(this.outlineFillMaterial);
    }

    // Token: 0x06000258 RID: 600 RVA: 0x0000B334 File Offset: 0x00009534
    private void Bake()
    {
        HashSet<Mesh> hashSet = new HashSet<Mesh>();
        foreach (MeshFilter meshFilter in base.GetComponentsInChildren<MeshFilter>())
        {
            if (hashSet.Add(meshFilter.sharedMesh))
            {
                List<Vector3> data = this.SmoothNormals(meshFilter.sharedMesh);
                this.bakeKeys.Add(meshFilter.sharedMesh);
                this.bakeValues.Add(new Outline.ListVector3
                {
                    data = data
                });
            }
        }
    }

    // Token: 0x06000259 RID: 601 RVA: 0x0000B3A8 File Offset: 0x000095A8
    private void LoadSmoothNormals()
    {
        foreach (MeshFilter meshFilter in base.GetComponentsInChildren<MeshFilter>())
        {
            if (Outline.registeredMeshes.Add(meshFilter.sharedMesh))
            {
                int num = this.bakeKeys.IndexOf(meshFilter.sharedMesh);
                List<Vector3> uvs = (num >= 0) ? this.bakeValues[num].data : this.SmoothNormals(meshFilter.sharedMesh);
                meshFilter.sharedMesh.SetUVs(3, uvs);
                Renderer component = meshFilter.GetComponent<Renderer>();
                if (component != null)
                {
                    this.CombineSubmeshes(meshFilter.sharedMesh, component.sharedMaterials);
                }
            }
        }
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in base.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (Outline.registeredMeshes.Add(skinnedMeshRenderer.sharedMesh))
            {
                skinnedMeshRenderer.sharedMesh.uv4 = new Vector2[skinnedMeshRenderer.sharedMesh.vertexCount];
                this.CombineSubmeshes(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials);
            }
        }
    }

    // Token: 0x0600025A RID: 602 RVA: 0x0000B4B4 File Offset: 0x000096B4
    private List<Vector3> SmoothNormals(Mesh mesh)
    {
        IEnumerable<IGrouping<Vector3, KeyValuePair<Vector3, int>>> enumerable = from pair in mesh.vertices.Select((Vector3 vertex, int index) => new KeyValuePair<Vector3, int>(vertex, index))
                                                                                 group pair by pair.Key;
        List<Vector3> list = new List<Vector3>(mesh.normals);
        foreach (IGrouping<Vector3, KeyValuePair<Vector3, int>> grouping in enumerable)
        {
            if (grouping.Count<KeyValuePair<Vector3, int>>() != 1)
            {
                Vector3 vector = Vector3.zero;
                foreach (KeyValuePair<Vector3, int> keyValuePair in grouping)
                {
                    vector += list[keyValuePair.Value];
                }
                vector.Normalize();
                foreach (KeyValuePair<Vector3, int> keyValuePair2 in grouping)
                {
                    list[keyValuePair2.Value] = vector;
                }
            }
        }
        return list;
    }

    // Token: 0x0600025B RID: 603 RVA: 0x0000B5FC File Offset: 0x000097FC
    private void CombineSubmeshes(Mesh mesh, Material[] materials)
    {
        if (mesh.subMeshCount == 1)
        {
            return;
        }
        if (mesh.subMeshCount > materials.Length)
        {
            return;
        }
        int subMeshCount = mesh.subMeshCount;
        mesh.subMeshCount = subMeshCount + 1;
        mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1);
    }

    // Token: 0x0600025C RID: 604 RVA: 0x0000B644 File Offset: 0x00009844
    private void UpdateMaterialProperties()
    {
        this.outlineFillMaterial.SetColor("_OutlineColor", this.outlineColor);
        switch (this.outlineMode)
        {
            case Outline.Mode.OutlineAll:
                this.outlineMaskMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.OutlineVisible:
                this.outlineMaskMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_ZTest", 4f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.OutlineHidden:
                this.outlineMaskMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_ZTest", 5f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.OutlineAndSilhouette:
                this.outlineMaskMaterial.SetFloat("_ZTest", 4f);
                this.outlineFillMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.SilhouetteOnly:
                this.outlineMaskMaterial.SetFloat("_ZTest", 4f);
                this.outlineFillMaterial.SetFloat("_ZTest", 5f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", 0f);
                return;
            default:
                return;
        }
    }

    // Token: 0x040002DA RID: 730
    private static HashSet<Mesh> registeredMeshes = new HashSet<Mesh>();

    // Token: 0x040002DB RID: 731
    [SerializeField]
    private Outline.Mode outlineMode;

    // Token: 0x040002DC RID: 732
    [SerializeField]
    private Color outlineColor = Color.white;

    // Token: 0x040002DD RID: 733
    [SerializeField]
    [Range(0f, 10f)]
    private float outlineWidth = 2f;

    // Token: 0x040002DE RID: 734
    [Header("Optional")]
    [SerializeField]
    [Tooltip("Precompute enabled: Per-vertex calculations are performed in the editor and serialized with the object. Precompute disabled: Per-vertex calculations are performed at runtime in Awake(). This may cause a pause for large meshes.")]
    private bool precomputeOutline;

    // Token: 0x040002DF RID: 735
    [SerializeField]
    [HideInInspector]
    private List<Mesh> bakeKeys = new List<Mesh>();

    // Token: 0x040002E0 RID: 736
    [SerializeField]
    [HideInInspector]
    private List<Outline.ListVector3> bakeValues = new List<Outline.ListVector3>();

    // Token: 0x040002E1 RID: 737
    private Renderer[] renderers;

    // Token: 0x040002E2 RID: 738
    private Material outlineMaskMaterial;

    // Token: 0x040002E3 RID: 739
    private Material outlineFillMaterial;

    // Token: 0x040002E4 RID: 740
    private bool needsUpdate;

    // Token: 0x020000D6 RID: 214
    public enum Mode
    {
        // Token: 0x04000496 RID: 1174
        OutlineAll,
        // Token: 0x04000497 RID: 1175
        OutlineVisible,
        // Token: 0x04000498 RID: 1176
        OutlineHidden,
        // Token: 0x04000499 RID: 1177
        OutlineAndSilhouette,
        // Token: 0x0400049A RID: 1178
        SilhouetteOnly
    }

    // Token: 0x020000D7 RID: 215
    [Serializable]
    private class ListVector3
    {
        // Token: 0x0400049B RID: 1179
        public List<Vector3> data;
    }
}

using System;
using UnityEngine;

// Token: 0x02000061 RID: 97
public class PauseMenu : MonoBehaviour
{
    // Token: 0x060001EF RID: 495 RVA: 0x0000930E File Offset: 0x0000750E
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!this.pausePanel.activeInHierarchy)
            {
                this.OnPause();
                return;
            }
            this.OnResume();
        }
    }

    // Token: 0x060001F0 RID: 496 RVA: 0x00009333 File Offset: 0x00007533
    public void OnPause()
    {
        this.pausePanel.SetActive(true);
        this.lockPanel.SetActive(false);
        this.UnlockCursor();
    }

    // Token: 0x060001F1 RID: 497 RVA: 0x00009353 File Offset: 0x00007553
    public void OnResume()
    {
        this.pausePanel.SetActive(false);
        if (this.cardGame != null && this.cardGame.activeInHierarchy)
        {
            Time.timeScale = 1f;
            return;
        }
        this.LockCursor();
    }

    // Token: 0x060001F2 RID: 498 RVA: 0x0000938D File Offset: 0x0000758D
    public void LockCursor()
    {
        CursorController.LockCursor();
        Time.timeScale = 1f;
    }

    // Token: 0x060001F3 RID: 499 RVA: 0x0000939E File Offset: 0x0000759E
    public void UnlockCursor()
    {
        CursorController.UnlockCursor();
        Time.timeScale = 0f;
    }

    // Token: 0x0400025E RID: 606
    public GameObject pausePanel;

    // Token: 0x0400025F RID: 607
    public GameObject lockPanel;

    // Token: 0x04000260 RID: 608
    public GameObject cardGame;
}

using System;
using UnityEngine;

// Token: 0x02000040 RID: 64
public class Pier : MonoBehaviour
{
    // Token: 0x06000157 RID: 343 RVA: 0x00006C72 File Offset: 0x00004E72
    private void Start()
    {
        this.LoadPresence();
        this.CheckPresence();
    }

    // Token: 0x06000158 RID: 344 RVA: 0x00006C80 File Offset: 0x00004E80
    private void LoadPresence()
    {
        this.presence = PlayerPrefs.GetString("Pier", "true");
    }

    // Token: 0x06000159 RID: 345 RVA: 0x00006C97 File Offset: 0x00004E97
    public void SavePresence()
    {
        PlayerPrefs.SetString("Pier", this.presence);
        this.CheckPresence();
    }

    // Token: 0x0600015A RID: 346 RVA: 0x00006CB0 File Offset: 0x00004EB0
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.boat.pierPos = this.pierPos;
            this.boat.pierIsNear = true;
            this.boat.boatText.gameObject.SetActive(true);
        }
    }

    // Token: 0x0600015B RID: 347 RVA: 0x00006CFD File Offset: 0x00004EFD
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.boat.pierIsNear = false;
            this.boat.boatText.gameObject.SetActive(false);
        }
    }

    // Token: 0x0600015C RID: 348 RVA: 0x00006D30 File Offset: 0x00004F30
    public void CheckPresence()
    {
        if (this.presence == "false")
        {
            this.boat.pierIsNear = false;
            this.boat.boatText.gameObject.SetActive(false);
            base.gameObject.SetActive(false);
            return;
        }
        if (this.presence == "true")
        {
            base.gameObject.SetActive(true);
        }
    }

    // Token: 0x04000199 RID: 409
    public BoatSwitch boat;

    // Token: 0x0400019A RID: 410
    public Transform pierPos;

    // Token: 0x0400019B RID: 411
    public bool lastPier;

    // Token: 0x0400019C RID: 412
    public string presence = "true";
}

using System;
using Cinemachine;
using UnityEngine;

// Token: 0x02000053 RID: 83
public class PlayerCamera : MonoBehaviour
{
    // Token: 0x060001AD RID: 429 RVA: 0x0000844D File Offset: 0x0000664D
    private void Start()
    {
        CursorController.LockCursor();
    }

    // Token: 0x060001AE RID: 430 RVA: 0x00008454 File Offset: 0x00006654
    private void Update()
    {
        if (!this.player.isStopped)
        {
            float num = Input.GetAxisRaw("Mouse X") * Time.deltaTime * this.sensitivityX;
            float num2 = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * this.sensitivityY;
            this.yRotation += num;
            this.xRotation -= num2;
            this.xRotation = Mathf.Clamp(this.xRotation, -90f, 90f);
            this.virtualCamera.transform.rotation = Quaternion.Euler(this.xRotation, this.yRotation, 0f);
            this.orientation.rotation = Quaternion.Euler(0f, this.yRotation, 0f);
        }
    }

    // Token: 0x060001AF RID: 431 RVA: 0x0000851E File Offset: 0x0000671E
    public void FlipPlayer()
    {
        this.yRotation += 180f;
    }

    // Token: 0x04000209 RID: 521
    public float sensitivityX;

    // Token: 0x0400020A RID: 522
    public float sensitivityY;

    // Token: 0x0400020B RID: 523
    public PlayerController player;

    // Token: 0x0400020C RID: 524
    public Transform orientation;

    // Token: 0x0400020D RID: 525
    public CinemachineVirtualCamera virtualCamera;

    // Token: 0x0400020E RID: 526
    private float xRotation;

    // Token: 0x0400020F RID: 527
    private float yRotation;
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Token: 0x02000054 RID: 84
public class PlayerController : MonoBehaviour
{
    // Token: 0x060001B1 RID: 433 RVA: 0x0000853C File Offset: 0x0000673C
    private void Start()
    {
        this.rb = base.GetComponent<Rigidbody>();
        this.rb.freezeRotation = true;
        this.readyToJump = true;
        if (GravityController.instance != null)
        {
            GravityController.instance.player = this;
            GravityController.instance.CheckGravity();
        }
        this.LoadPosition();
    }

    // Token: 0x060001B2 RID: 434 RVA: 0x00008590 File Offset: 0x00006790
    private void Update()
    {
        if (!this.isStopped)
        {
            this.grounded = Physics.Raycast(base.transform.position, Vector3.down, this.groundedValue * 0.5f + 0.01f, this.whatIsGround);
            this.MyInput();
            this.SpeedControl();
            if (this.grounded)
            {
                this.rb.drag = this.groundDrag;
                return;
            }
            this.rb.drag = 0f;
        }
    }

    // Token: 0x060001B3 RID: 435 RVA: 0x00008613 File Offset: 0x00006813
    private void FixedUpdate()
    {
        if (!this.isStopped)
        {
            this.MovePlayer();
        }
    }

    // Token: 0x060001B4 RID: 436 RVA: 0x00008624 File Offset: 0x00006824
    private void MyInput()
    {
        this.horizontalInput = Input.GetAxisRaw("Horizontal");
        this.verticalInput = Input.GetAxisRaw("Vertical");
        if (Input.GetKey(this.jumpKey) && this.readyToJump && this.grounded)
        {
            this.readyToJump = false;
            this.Jump();
            base.Invoke("ResetJump", this.jumpCooldown);
        }
    }

    // Token: 0x060001B5 RID: 437 RVA: 0x0000868C File Offset: 0x0000688C
    private void MovePlayer()
    {
        this.moveDirection = this.orientation.forward * this.verticalInput + this.orientation.right * this.horizontalInput;
        if (this.grounded)
        {
            this.rb.AddForce(this.moveDirection.normalized * this.moveSpeed * 10f, ForceMode.Force);
            return;
        }
        if (!this.grounded)
        {
            this.rb.AddForce(this.moveDirection.normalized * this.moveSpeed * 10f * this.airMultiplier, ForceMode.Force);
        }
    }

    // Token: 0x060001B6 RID: 438 RVA: 0x00008744 File Offset: 0x00006944
    private void SpeedControl()
    {
        Vector3 vector = new Vector3(this.rb.velocity.x, 0f, this.rb.velocity.z);
        if (vector.magnitude > this.moveSpeed)
        {
            Vector3 vector2 = vector.normalized * this.moveSpeed;
            this.rb.velocity = new Vector3(vector2.x, this.rb.velocity.y, vector2.z);
        }
    }

    // Token: 0x060001B7 RID: 439 RVA: 0x000087CC File Offset: 0x000069CC
    private void Jump()
    {
        this.rb.velocity = new Vector3(this.rb.velocity.x, 0f, this.rb.velocity.z);
        this.rb.AddForce(base.transform.up * this.jumpForce, ForceMode.Impulse);
    }

    // Token: 0x060001B8 RID: 440 RVA: 0x00008830 File Offset: 0x00006A30
    private void ResetJump()
    {
        this.readyToJump = true;
    }

    // Token: 0x060001B9 RID: 441 RVA: 0x0000883C File Offset: 0x00006A3C
    public void SavePosition()
    {
        this.x = base.transform.position.x;
        this.y = base.transform.position.y;
        this.z = base.transform.position.z;
        if (SceneManager.GetActiveScene().name == this.startSceneName)
        {
            PlayerPrefs.SetFloat("x", this.x);
            PlayerPrefs.SetFloat("y", this.y);
            PlayerPrefs.SetFloat("z", this.z);
        }
        else
        {
            PlayerPrefs.SetFloat("xMoon", this.x);
            PlayerPrefs.SetFloat("yMoon", this.y);
            PlayerPrefs.SetFloat("zMoon", this.z);
        }
        PlayerPrefs.SetString("Gun", this.hasGun);
    }

    // Token: 0x060001BA RID: 442 RVA: 0x00008918 File Offset: 0x00006B18
    public void LoadPosition()
    {
        if (SceneManager.GetActiveScene().name == this.startSceneName)
        {
            this.x = PlayerPrefs.GetFloat("x", 57.47f);
            this.y = PlayerPrefs.GetFloat("y", 20.69f);
            this.z = PlayerPrefs.GetFloat("z", 40.24f);
        }
        else
        {
            this.x = PlayerPrefs.GetFloat("xMoon", 2.931f);
            this.y = PlayerPrefs.GetFloat("yMoon", 5.168f);
            this.z = PlayerPrefs.GetFloat("zMoon", -8.655f);
        }
        this.hasGun = PlayerPrefs.GetString("Gun", "false");
        Vector3 position = new Vector3(this.x, this.y, this.z);
        if (this.hasGun == "true")
        {
            this.gun.SetActive(true);
            this.gunText.SetActive(true);
            this.aim.SetActive(true);
        }
        else
        {
            this.gun.SetActive(false);
            this.gunText.SetActive(false);
            this.aim.SetActive(false);
        }
        base.transform.position = position;
    }

    // Token: 0x060001BB RID: 443 RVA: 0x00008A55 File Offset: 0x00006C55
    public void SetupDialogue(bool mode)
    {
        this.isStopped = mode;
        if (mode)
        {
            CursorController.UnlockCursor();
            return;
        }
        CursorController.LockCursor();
    }

    // Token: 0x04000210 RID: 528
    [Header("Movement")]
    public float moveSpeed;

    // Token: 0x04000211 RID: 529
    public Transform orientation;

    // Token: 0x04000212 RID: 530
    public float groundDrag;

    // Token: 0x04000213 RID: 531
    public float groundedValue;

    // Token: 0x04000214 RID: 532
    public float jumpForce;

    // Token: 0x04000215 RID: 533
    public float jumpCooldown;

    // Token: 0x04000216 RID: 534
    public float airMultiplier;

    // Token: 0x04000217 RID: 535
    public float x;

    // Token: 0x04000218 RID: 536
    public float y;

    // Token: 0x04000219 RID: 537
    public float z;

    // Token: 0x0400021A RID: 538
    public string hasGun = "false";

    // Token: 0x0400021B RID: 539
    public GameObject gun;

    // Token: 0x0400021C RID: 540
    public GameObject aim;

    // Token: 0x0400021D RID: 541
    public GameObject gunText;

    // Token: 0x0400021E RID: 542
    private bool readyToJump;

    // Token: 0x0400021F RID: 543
    [Header("Ground Check")]
    public float playerHeight;

    // Token: 0x04000220 RID: 544
    public LayerMask whatIsGround;

    // Token: 0x04000221 RID: 545
    public bool grounded;

    // Token: 0x04000222 RID: 546
    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;

    // Token: 0x04000223 RID: 547
    private float horizontalInput;

    // Token: 0x04000224 RID: 548
    private float verticalInput;

    // Token: 0x04000225 RID: 549
    [HideInInspector]
    public bool isStopped;

    // Token: 0x04000226 RID: 550
    [Header("Scenes")]
    public string startSceneName;

    // Token: 0x04000227 RID: 551
    private Vector3 moveDirection;

    // Token: 0x04000228 RID: 552
    private Rigidbody rb;
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Token: 0x02000055 RID: 85
public class PlayerHealth : MonoBehaviour
{
    // Token: 0x060001BD RID: 445 RVA: 0x00008A87 File Offset: 0x00006C87
    private void Start()
    {
        this.currentHealth = this.maxHealth;
        this.currectHealCooldown = this.healCooldownAfterDamage;
    }

    // Token: 0x060001BE RID: 446 RVA: 0x00008AA4 File Offset: 0x00006CA4
    private void Update()
    {
        this.bloodOverlay.color = new Color(this.bloodOverlay.color.r, this.bloodOverlay.color.g, this.bloodOverlay.color.b, 1f - this.currentHealth / this.maxHealth);
        if (this.currectHealCooldown > 0f)
        {
            this.currectHealCooldown -= Time.deltaTime;
        }
        else
        {
            this.canHeal = true;
        }
        if (Time.realtimeSinceStartup < this.nextHealthUpdateTime)
        {
            return;
        }
        this.nextHealthUpdateTime = Time.realtimeSinceStartup + this.healSpeed;
        if (this.currentHealth < this.maxHealth && this.canHeal)
        {
            this.currentHealth += this.healAmount;
        }
    }

    // Token: 0x060001BF RID: 447 RVA: 0x00008B78 File Offset: 0x00006D78
    public void TakeDamage(float damage)
    {
        this.canHeal = false;
        this.currectHealCooldown = this.healCooldownAfterDamage;
        this.currentHealth -= damage;
        if (this.currentHealth <= 0f)
        {
            Object.FindObjectOfType<MusicManager>().EnableMusic(0);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // Token: 0x04000229 RID: 553
    public Image bloodOverlay;

    // Token: 0x0400022A RID: 554
    public float maxHealth = 100f;

    // Token: 0x0400022B RID: 555
    public float currentHealth = 100f;

    // Token: 0x0400022C RID: 556
    private float nextHealthUpdateTime;

    // Token: 0x0400022D RID: 557
    public float healSpeed = 1f;

    // Token: 0x0400022E RID: 558
    public float healAmount = 1f;

    // Token: 0x0400022F RID: 559
    public float healCooldownAfterDamage = 5f;

    // Token: 0x04000230 RID: 560
    public float currectHealCooldown;

    // Token: 0x04000231 RID: 561
    private bool canHeal = true;
}

using System;
using UnityEngine;

// Token: 0x0200007C RID: 124
public class PlayerMovement : MonoBehaviour
{
    // Token: 0x06000248 RID: 584 RVA: 0x0000AF78 File Offset: 0x00009178
    private void Update()
    {
        this.isGrounded = Physics.CheckSphere(this.groundCheck.position, this.groundDistance, this.groundMask);
        if (this.isGrounded && this.velocity.y < 0f)
        {
            this.velocity.y = -2f;
        }
        if (Input.GetKey("left shift") && this.isGrounded)
        {
            this.speed = 10f;
        }
        else
        {
            this.speed = 5f;
        }
        float axis = Input.GetAxis("Horizontal");
        float axis2 = Input.GetAxis("Vertical");
        Vector3 a = base.transform.right * axis + base.transform.forward * axis2;
        this.controller.Move(a * this.speed * Time.deltaTime);
        if (Input.GetButtonDown("Jump") && this.isGrounded)
        {
            this.velocity.y = Mathf.Sqrt(this.jumpHeight * -2f * this.gravity);
        }
        this.velocity.y = this.velocity.y + this.gravity * Time.deltaTime;
        this.controller.Move(this.velocity * Time.deltaTime);
    }

    // Token: 0x040002D1 RID: 721
    public CharacterController controller;

    // Token: 0x040002D2 RID: 722
    public float speed = 5f;

    // Token: 0x040002D3 RID: 723
    public float gravity = -9.18f;

    // Token: 0x040002D4 RID: 724
    public float jumpHeight = 3f;

    // Token: 0x040002D5 RID: 725
    public Transform groundCheck;

    // Token: 0x040002D6 RID: 726
    public float groundDistance = 0.4f;

    // Token: 0x040002D7 RID: 727
    public LayerMask groundMask;

    // Token: 0x040002D8 RID: 728
    private Vector3 velocity;

    // Token: 0x040002D9 RID: 729
    private bool isGrounded;
}

using System;
using UnityEngine;

// Token: 0x0200006A RID: 106
public class ProjectileAddon : MonoBehaviour
{
    // Token: 0x0600021A RID: 538 RVA: 0x0000A3DE File Offset: 0x000085DE
    private void Start()
    {
        this.rb = base.GetComponent<Rigidbody>();
    }

    // Token: 0x0600021B RID: 539 RVA: 0x0000A3EC File Offset: 0x000085EC
    private void OnCollisionEnter(Collision collision)
    {
        if (this.targetHit)
        {
            return;
        }
        this.targetHit = true;
        if (collision.gameObject.GetComponent<BasicEnemy>() != null)
        {
            collision.gameObject.GetComponent<BasicEnemy>().TakeDamage(this.damage);
            Object.Destroy(base.gameObject);
        }
        this.rb.isKinematic = true;
        base.transform.SetParent(collision.transform);
    }

    // Token: 0x04000289 RID: 649
    public int damage;

    // Token: 0x0400028A RID: 650
    private Rigidbody rb;

    // Token: 0x0400028B RID: 651
    private bool targetHit;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000030 RID: 48
public class ProjectileGun : MonoBehaviour
{
    // Token: 0x060000F9 RID: 249 RVA: 0x00005506 File Offset: 0x00003706
    private void Awake()
    {
        this._animator = base.GetComponent<Animator>();
        this.bulletsLeft = this.magazineSize;
        this.readyToShoot = true;
    }

    // Token: 0x060000FA RID: 250 RVA: 0x00005527 File Offset: 0x00003727
    private void Update()
    {
        this.MyInput();
    }

    // Token: 0x060000FB RID: 251 RVA: 0x00005530 File Offset: 0x00003730
    private void MyInput()
    {
        if (this.allowButtonHold)
        {
            this.shooting = Input.GetKey(KeyCode.Mouse0);
        }
        else
        {
            this.shooting = Input.GetKeyDown(KeyCode.Mouse0);
        }
        if (Input.GetKeyDown(KeyCode.R) && this.bulletsLeft < this.magazineSize && !this.reloading)
        {
            this.Reload();
        }
        if (this.readyToShoot && this.shooting && !this.reloading && this.bulletsLeft <= 0)
        {
            this.Reload();
        }
        if (this.readyToShoot && this.shooting && !this.reloading && this.bulletsLeft > 0)
        {
            this.bulletsShot = 0;
            this.Shoot();
        }
    }

    // Token: 0x060000FC RID: 252 RVA: 0x000055E4 File Offset: 0x000037E4
    private void Shoot()
    {
        this.readyToShoot = false;
        this.cylinderAnimator.SetTrigger("hasShot");
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit raycastHit;
        Vector3 point;
        if (Physics.Raycast(ray, out raycastHit))
        {
            point = raycastHit.point;
        }
        else
        {
            point = ray.GetPoint(75f);
        }
        Vector3 a = point - this.attackPoint.position;
        float x = Random.Range(-this.spread, this.spread);
        float y = Random.Range(-this.spread, this.spread);
        Vector3 vector = a + new Vector3(x, y, 0f);
        GameObject gameObject = Object.Instantiate<GameObject>(this.bullet, this.attackPoint.position, Quaternion.identity);
        gameObject.transform.forward = vector.normalized;
        gameObject.GetComponent<Rigidbody>().AddForce(vector.normalized * this.shootForce, ForceMode.Impulse);
        gameObject.GetComponent<Rigidbody>().AddForce(Camera.main.transform.up * this.upwardForce, ForceMode.Impulse);
        if (this.muzzleFlash != null)
        {
            Object.Instantiate<GameObject>(this.muzzleFlash, this.attackPoint.position, Quaternion.identity).transform.parent = this.attackPoint;
        }
        this.bulletsLeft--;
        this.bulletsShot++;
        this.UpdateText();
        if (this.allowInvoke)
        {
            base.Invoke("ResetShot", this.timeBetweenShooting);
            this.allowInvoke = false;
            this.playerRb.AddForce(-vector.normalized * this.recoilForce, ForceMode.Impulse);
        }
        if (this.bulletsShot < this.bulletsPerTap && this.bulletsLeft > 0)
        {
            base.Invoke("Shoot", this.timeBetweenShots);
        }
    }

    // Token: 0x060000FD RID: 253 RVA: 0x000057CA File Offset: 0x000039CA
    private void ResetShot()
    {
        this.readyToShoot = true;
        this.allowInvoke = true;
    }

    // Token: 0x060000FE RID: 254 RVA: 0x000057DC File Offset: 0x000039DC
    private void Reload()
    {
        this._animator.SetTrigger("startReloading");
        this.reloading = true;
        this.ammunitionDisplay.enabled = false;
        this.ammunitionDisplay.transform.GetChild(0).gameObject.SetActive(true);
        base.Invoke("ReloadFinished", this.reloadTime);
    }

    // Token: 0x060000FF RID: 255 RVA: 0x0000583C File Offset: 0x00003A3C
    private void ReloadFinished()
    {
        this.ammunitionDisplay.enabled = true;
        this.ammunitionDisplay.transform.GetChild(0).gameObject.SetActive(false);
        this.bulletsLeft = this.magazineSize;
        this.reloading = false;
        this.UpdateText();
    }

    // Token: 0x06000100 RID: 256 RVA: 0x0000588C File Offset: 0x00003A8C
    private void UpdateText()
    {
        if (this.ammunitionDisplay != null)
        {
            this.ammunitionDisplay.SetText((this.bulletsLeft / this.bulletsPerTap).ToString() + " / " + (this.magazineSize / this.bulletsPerTap).ToString(), true);
        }
    }

    // Token: 0x06000101 RID: 257 RVA: 0x000058E7 File Offset: 0x00003AE7
    public void TriggerCylinderReload()
    {
        this.cylinderAnimator.SetTrigger("fullReload");
    }

    // Token: 0x0400010F RID: 271
    public GameObject bullet;

    // Token: 0x04000110 RID: 272
    public float shootForce;

    // Token: 0x04000111 RID: 273
    public float upwardForce;

    // Token: 0x04000112 RID: 274
    public float timeBetweenShooting;

    // Token: 0x04000113 RID: 275
    public float spread;

    // Token: 0x04000114 RID: 276
    public float reloadTime;

    // Token: 0x04000115 RID: 277
    public float timeBetweenShots;

    // Token: 0x04000116 RID: 278
    public int magazineSize;

    // Token: 0x04000117 RID: 279
    public int bulletsPerTap;

    // Token: 0x04000118 RID: 280
    public bool allowButtonHold;

    // Token: 0x04000119 RID: 281
    private int bulletsLeft;

    // Token: 0x0400011A RID: 282
    private int bulletsShot;

    // Token: 0x0400011B RID: 283
    public Rigidbody playerRb;

    // Token: 0x0400011C RID: 284
    public float recoilForce;

    // Token: 0x0400011D RID: 285
    private bool shooting;

    // Token: 0x0400011E RID: 286
    private bool readyToShoot;

    // Token: 0x0400011F RID: 287
    private bool reloading;

    // Token: 0x04000120 RID: 288
    public Transform attackPoint;

    // Token: 0x04000121 RID: 289
    public GameObject muzzleFlash;

    // Token: 0x04000122 RID: 290
    public GameObject gunText;

    // Token: 0x04000123 RID: 291
    public TextMeshProUGUI ammunitionDisplay;

    // Token: 0x04000124 RID: 292
    public Animator cylinderAnimator;

    // Token: 0x04000125 RID: 293
    public bool allowInvoke = true;

    // Token: 0x04000126 RID: 294
    private Animator _animator;
}

using System;
using UnityEngine;

// Token: 0x0200002D RID: 45
public class RunOver : MonoBehaviour
{
    // Token: 0x060000ED RID: 237 RVA: 0x00005372 File Offset: 0x00003572
    private void Start()
    {
        this.gateAnimator.SetBool("hasMushrooms", true);
    }

    // Token: 0x060000EE RID: 238 RVA: 0x00005385 File Offset: 0x00003585
    private void Update()
    {
        this.hasFinishedRun = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        if (this.hasFinishedRun)
        {
            this.EndRun();
        }
    }

    // Token: 0x060000EF RID: 239 RVA: 0x000053BC File Offset: 0x000035BC
    public void EndRun()
    {
        this.enemyRun.TakeDamage(50);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.FindObjectOfType<PlayerController>().moveSpeed = 3.5f;
        this.gateAnimator.SetBool("pastGate", true);
        base.enabled = false;
    }

    // Token: 0x060000F0 RID: 240 RVA: 0x00005408 File Offset: 0x00003608
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x04000102 RID: 258
    public Animator gateAnimator;

    // Token: 0x04000103 RID: 259
    public Enemy enemyRun;

    // Token: 0x04000104 RID: 260
    public LayerMask whatIsPlayer;

    // Token: 0x04000105 RID: 261
    public float sightRange;

    // Token: 0x04000106 RID: 262
    private bool hasFinishedRun;
}

using System;
using System.Collections;
using UnityEngine;

// Token: 0x0200006B RID: 107
public class Sable : MonoBehaviour
{
    // Token: 0x0600021D RID: 541 RVA: 0x0000A464 File Offset: 0x00008664
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            int num = Random.Range(1, 5);
            int num2 = Random.Range(0, this.swingSounds.Length);
            this.weaponAnimator.Play(num.ToString());
            Object.Instantiate<GameObject>(this.swingSounds[num2], base.transform.position, Quaternion.identity);
            this.clickChecker = this.ClickCheck();
            base.StartCoroutine(this.clickChecker);
        }
    }

    // Token: 0x0600021E RID: 542 RVA: 0x0000A4DC File Offset: 0x000086DC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Enemy enemy;
            if (other.transform.root.TryGetComponent<Enemy>(out enemy) && this.clickChecker != null)
            {
                Debug.Log("NYAH");
                enemy.TakeDamage(this.damage);
                this.InstantiateSounds(other);
                base.StopCoroutine(this.clickChecker);
                this.clickChecker = null;
                return;
            }
            Shailushay shailushay;
            if (other.transform.root.TryGetComponent<Shailushay>(out shailushay) && this.clickChecker != null)
            {
                shailushay.TakeDamage(this.damage);
                this.InstantiateSounds(other);
                base.StopCoroutine(this.clickChecker);
                this.clickChecker = null;
            }
        }
    }

    // Token: 0x0600021F RID: 543 RVA: 0x0000A588 File Offset: 0x00008788
    private void InstantiateSounds(Collider other)
    {
        int num = Random.Range(0, this.damageSounds.Length);
        Object.Instantiate<GameObject>(this.damageSounds[num], base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.damageEffect, other.ClosestPointOnBounds(base.transform.position), Quaternion.identity);
    }

    // Token: 0x06000220 RID: 544 RVA: 0x0000A5E4 File Offset: 0x000087E4
    private IEnumerator ClickCheck()
    {
        yield return new WaitForSeconds(0.2f);
        this.clickChecker = null;
        yield break;
    }

    // Token: 0x0400028C RID: 652
    public int damage;

    // Token: 0x0400028D RID: 653
    public GameObject[] damageSounds;

    // Token: 0x0400028E RID: 654
    public Animator weaponAnimator;

    // Token: 0x0400028F RID: 655
    public GameObject[] swingSounds;

    // Token: 0x04000290 RID: 656
    public GameObject damageEffect;

    // Token: 0x04000291 RID: 657
    private Vector3 sparksDirection;

    // Token: 0x04000292 RID: 658
    private IEnumerator clickChecker;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000062 RID: 98
public class SaveDropdownValue : MonoBehaviour
{
    // Token: 0x060001F5 RID: 501 RVA: 0x000093B7 File Offset: 0x000075B7
    private void Awake()
    {
        this.dropdown = base.GetComponent<TMP_Dropdown>();
        this.dropdown.onValueChanged.AddListener(delegate (int index)
        {
            PlayerPrefs.SetInt("optionvalue", this.dropdown.value);
            PlayerPrefs.Save();
        });
    }

    // Token: 0x060001F6 RID: 502 RVA: 0x000093E1 File Offset: 0x000075E1
    private void Start()
    {
        this.dropdown.value = PlayerPrefs.GetInt("optionvalue", 2);
    }

    // Token: 0x04000261 RID: 609
    private const string PrefName = "optionvalue";

    // Token: 0x04000262 RID: 610
    private TMP_Dropdown dropdown;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Token: 0x02000057 RID: 87
public class SceneLoader : MonoBehaviour
{
    // Token: 0x060001C6 RID: 454 RVA: 0x00008D3D File Offset: 0x00006F3D
    private void OnEnable()
    {
        this.LoadScene(this.sceneIndex);
    }

    // Token: 0x060001C7 RID: 455 RVA: 0x00008D4B File Offset: 0x00006F4B
    public void LoadScene(int index)
    {
        if (index == 2)
        {
            PlayerPrefs.SetInt("Scene", 2);
        }
        base.StartCoroutine(this.LoadAsync(index));
    }

    // Token: 0x060001C8 RID: 456 RVA: 0x00008D6A File Offset: 0x00006F6A
    public IEnumerator LoadAsync(int index)
    {
        this.progressSlider.value = 0f;
        this.LoaderUI.SetActive(true);
        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(index);
        asyncOperation.allowSceneActivation = false;
        float progress = 0f;
        while (!asyncOperation.isDone)
        {
            progress = Mathf.MoveTowards(progress, asyncOperation.progress, Time.deltaTime);
            this.progressSlider.value = progress;
            if (progress >= 0.9f)
            {
                this.progressSlider.value = 1f;
                asyncOperation.allowSceneActivation = true;
            }
            yield return null;
        }
        yield break;
    }

    // Token: 0x0400023A RID: 570
    public int sceneIndex;

    // Token: 0x0400023B RID: 571
    public GameObject LoaderUI;

    // Token: 0x0400023C RID: 572
    public Slider progressSlider;
}

using System;
using UnityEngine;

// Token: 0x0200001B RID: 27
public class SceneSaver : MonoBehaviour
{
    // Token: 0x06000074 RID: 116 RVA: 0x00003546 File Offset: 0x00001746
    private void Start()
    {
    }

    // Token: 0x06000075 RID: 117 RVA: 0x00003548 File Offset: 0x00001748
    private void Update()
    {
    }
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000063 RID: 99
public class SetQuality : MonoBehaviour
{
    // Token: 0x060001F9 RID: 505 RVA: 0x0000941D File Offset: 0x0000761D
    public void SetQualityLevel(int index)
    {
        QualitySettings.SetQualityLevel(index, false);
        this.settings.SaveQuality(index);
    }

    // Token: 0x04000263 RID: 611
    [SerializeField]
    private TMP_Dropdown qualityDropdown;

    // Token: 0x04000264 RID: 612
    [SerializeField]
    private GameSettings settings;
}

using System;
using UnityEngine;

// Token: 0x02000041 RID: 65
public class ShaiActivator : MonoBehaviour
{
    // Token: 0x0600015E RID: 350 RVA: 0x00006DAF File Offset: 0x00004FAF
    private void Start()
    {
        this.music = Object.FindObjectOfType<MusicManager>();
    }

    // Token: 0x0600015F RID: 351 RVA: 0x00006DBC File Offset: 0x00004FBC
    public void OnPick()
    {
        this.shaiBoss.SetActive(true);
        this.gate.SetActive(true);
        if (!(this.music.audioSource.clip != this.music.musicVariants[5]) && !(this.music.audioSource.clip != this.music.musicVariants[6]))
        {
            this.music.EnableMusic(0);
            return;
        }
        if (Random.Range(0, 10) == 9)
        {
            Debug.Log("POVEZLOOO");
            this.music.EnableMusic(6);
            return;
        }
        this.music.EnableMusic(5);
    }

    // Token: 0x0400019D RID: 413
    public GameObject shaiBoss;

    // Token: 0x0400019E RID: 414
    public GameObject gate;

    // Token: 0x0400019F RID: 415
    private MusicManager music;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Token: 0x02000026 RID: 38
public class Shailushay : MonoBehaviour
{
    // Token: 0x060000CA RID: 202 RVA: 0x00004BBE File Offset: 0x00002DBE
    private void Awake()
    {
        this.agent = base.GetComponent<NavMeshAgent>();
        this.animator = base.GetComponentInChildren<Animator>();
        this.health = this.startHealth;
    }

    // Token: 0x060000CB RID: 203 RVA: 0x00004BE4 File Offset: 0x00002DE4
    private void Start()
    {
        this.healthSlider.gameObject.SetActive(true);
        this.damage = this.startDamage;
    }

    // Token: 0x060000CC RID: 204 RVA: 0x00004C04 File Offset: 0x00002E04
    private void Update()
    {
        this.healthSlider.value = this.health / this.startHealth;
        this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
        if (!this.playerInSightRange && !this.playerInAttackRange)
        {
            this.Patroling();
        }
        if (this.playerInSightRange && !this.playerInAttackRange)
        {
            this.ChasePlayer();
        }
        if (this.playerInAttackRange && this.playerInSightRange)
        {
            this.AttackPlayer();
        }
    }

    // Token: 0x060000CD RID: 205 RVA: 0x00004CBC File Offset: 0x00002EBC
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x060000CE RID: 206 RVA: 0x00004D20 File Offset: 0x00002F20
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x060000CF RID: 207 RVA: 0x00004DC6 File Offset: 0x00002FC6
    private void ChasePlayer()
    {
        this.agent.SetDestination(this.player.position);
        if (!this.isAttacking)
        {
            this.isAttacking = true;
            base.StartCoroutine(this.Rest());
        }
    }

    // Token: 0x060000D0 RID: 208 RVA: 0x00004DFC File Offset: 0x00002FFC
    private void AttackPlayer()
    {
        this.agent.SetDestination(base.transform.position);
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x060000D1 RID: 209 RVA: 0x00004E98 File Offset: 0x00003098
    public void ChooseMoveset()
    {
        int num = Random.Range(0, 5);
        if (num == 0)
        {
            base.StartCoroutine(this.SpawnGroupSticks(5, 0.25f));
            if (this.health < 45f)
            {
                for (int i = 0; i < 7; i++)
                {
                    base.StartCoroutine(this.SummonLightning());
                }
                return;
            }
        }
        else if (num == 1)
        {
            base.StartCoroutine(this.SpawnGroupSticks(8, 0.15f));
            if (this.health < 45f)
            {
                for (int j = 0; j < 7; j++)
                {
                    base.StartCoroutine(this.SummonLightning());
                }
                return;
            }
        }
        else
        {
            if (num == 2 || num == 3)
            {
                base.StartCoroutine(this.SpeedUp());
                return;
            }
            if (num == 4)
            {
                base.StartCoroutine(this.SummonMushrooms());
            }
        }
    }

    // Token: 0x060000D2 RID: 210 RVA: 0x00004F4F File Offset: 0x0000314F
    public IEnumerator SpawnGroupSticks(int totalSticks, float cooldown)
    {
        int numberOfSticks = 0;
        while (numberOfSticks <= totalSticks)
        {
            this.SpawnStick();
            int num = numberOfSticks;
            numberOfSticks = num + 1;
            yield return new WaitForSeconds(cooldown);
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x060000D3 RID: 211 RVA: 0x00004F6C File Offset: 0x0000316C
    public void SpawnStick()
    {
        Object.Instantiate<GameObject>(this.stick, this.attackPos.position, this.attackPos.rotation);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
    }

    // Token: 0x060000D4 RID: 212 RVA: 0x00004FAC File Offset: 0x000031AC
    public IEnumerator SummonLightning()
    {
        Vector3 lightningPos = this.RandomPointInBox(new Vector3(15f, 0f, 15f));
        Object.Instantiate<GameObject>(this.preLightning, lightningPos, Quaternion.identity);
        yield return new WaitForSeconds(1f);
        Object.Instantiate<GameObject>(this.lightning, lightningPos, Quaternion.identity);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
        yield break;
    }

    // Token: 0x060000D5 RID: 213 RVA: 0x00004FBB File Offset: 0x000031BB
    public IEnumerator SummonMushrooms()
    {
        if (this.health > 45f)
        {
            this.easySpawner.enabled = true;
            yield return new WaitForSeconds(2f);
            this.easySpawner.enabled = false;
        }
        else
        {
            this.hardSpawner.enabled = true;
            yield return new WaitForSeconds(3f);
            this.hardSpawner.enabled = false;
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x060000D6 RID: 214 RVA: 0x00004FCA File Offset: 0x000031CA
    public IEnumerator Rest()
    {
        this.restCount++;
        if (this.restCount > 2)
        {
            this.animator.SetBool("isStanding", true);
            this.agent.speed = 0f;
            this.damage = 0;
            yield return new WaitForSeconds(4.5f);
            this.restCount = 0;
            this.animator.SetBool("isStanding", false);
            this.damage = this.startDamage;
            this.agent.speed = this.startSpeed;
        }
        this.ChooseMoveset();
        yield break;
    }

    // Token: 0x060000D7 RID: 215 RVA: 0x00004FD9 File Offset: 0x000031D9
    public IEnumerator SpeedUp()
    {
        this.restCount += 3;
        this.agent.speed = this.startSpeed * 2.4f;
        this.animator.SetFloat("runSpeed", 2f);
        yield return new WaitForSeconds(7f);
        this.agent.speed = this.startSpeed;
        this.animator.SetFloat("runSpeed", 1f);
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x060000D8 RID: 216 RVA: 0x00004FE8 File Offset: 0x000031E8
    private Vector3 RandomPointInBox(Vector3 size)
    {
        return new Vector3(this.player.position.x, 19f, this.player.position.z) + new Vector3((Random.value - 0.5f) * size.x, (Random.value - 0.5f) * size.y, (Random.value - 0.5f) * size.z);
    }

    // Token: 0x060000D9 RID: 217 RVA: 0x0000505F File Offset: 0x0000325F
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x060000DA RID: 218 RVA: 0x00005068 File Offset: 0x00003268
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x060000DB RID: 219 RVA: 0x00005098 File Offset: 0x00003298
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.finalMushroom, base.transform.position, Quaternion.identity);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x060000DC RID: 220 RVA: 0x000050F4 File Offset: 0x000032F4
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x040000D3 RID: 211
    public LayerMask whatIsGround;

    // Token: 0x040000D4 RID: 212
    public LayerMask whatIsPlayer;

    // Token: 0x040000D5 RID: 213
    public Vector3 walkPoint;

    // Token: 0x040000D6 RID: 214
    private bool walkPointSet;

    // Token: 0x040000D7 RID: 215
    public float walkPointRange;

    // Token: 0x040000D8 RID: 216
    public float timeBetweenAttacks;

    // Token: 0x040000D9 RID: 217
    private bool isAttacking;

    // Token: 0x040000DA RID: 218
    private bool alreadyAttacked;

    // Token: 0x040000DB RID: 219
    public float sightRange;

    // Token: 0x040000DC RID: 220
    public float attackRange;

    // Token: 0x040000DD RID: 221
    public bool playerInSightRange;

    // Token: 0x040000DE RID: 222
    public bool playerInAttackRange;

    // Token: 0x040000DF RID: 223
    public float startHealth;

    // Token: 0x040000E0 RID: 224
    public float health;

    // Token: 0x040000E1 RID: 225
    public int startDamage;

    // Token: 0x040000E2 RID: 226
    public float startSpeed;

    // Token: 0x040000E3 RID: 227
    private int damage;

    // Token: 0x040000E4 RID: 228
    private int restCount;

    // Token: 0x040000E5 RID: 229
    public GameObject stick;

    // Token: 0x040000E6 RID: 230
    public GameObject preLightning;

    // Token: 0x040000E7 RID: 231
    public GameObject lightning;

    // Token: 0x040000E8 RID: 232
    public Transform attackPos;

    // Token: 0x040000E9 RID: 233
    public Transform lightningCenter;

    // Token: 0x040000EA RID: 234
    public GameObject magicSound;

    // Token: 0x040000EB RID: 235
    public GameObject deathEffect;

    // Token: 0x040000EC RID: 236
    public GameObject finalMushroom;

    // Token: 0x040000ED RID: 237
    public Slider healthSlider;

    // Token: 0x040000EE RID: 238
    private NavMeshAgent agent;

    // Token: 0x040000EF RID: 239
    public Transform player;

    // Token: 0x040000F0 RID: 240
    private Animator animator;

    // Token: 0x040000F1 RID: 241
    private GameObject newStick;

    // Token: 0x040000F2 RID: 242
    private CurveSpawner easySpawner;

    // Token: 0x040000F3 RID: 243
    private CurveSpawner hardSpawner;

    // Token: 0x040000F4 RID: 244
    private Vector3 stickDirection;
}

using System;
using UnityEngine;

// Token: 0x0200002E RID: 46
public class ShipScreamer : MonoBehaviour
{
    // Token: 0x060000F2 RID: 242 RVA: 0x00005432 File Offset: 0x00003632
    private void OnTriggerEnter(Collider other)
    {
        if (this.sable.activeInHierarchy)
        {
            this.screamer.SetActive(true);
        }
    }

    // Token: 0x04000107 RID: 263
    public GameObject screamer;

    // Token: 0x04000108 RID: 264
    public GameObject sable;
}

using System;
using UnityEngine;

// Token: 0x02000027 RID: 39
public class SmallChanceSpawner : MonoBehaviour
{
    // Token: 0x060000DE RID: 222 RVA: 0x00005149 File Offset: 0x00003349
    private void Start()
    {
        if (Random.Range(0, 5) == 0)
        {
            Object.Instantiate<GameObject>(this.spawnable, base.transform.position, Quaternion.identity);
        }
    }

    // Token: 0x040000F5 RID: 245
    public GameObject spawnable;
}

using System;
using UnityEngine;

// Token: 0x0200005B RID: 91
public class SoundManager : MonoBehaviour
{
    // Token: 0x060001D3 RID: 467 RVA: 0x00008E84 File Offset: 0x00007084
    private void Start()
    {
        if (this.isEnemy)
        {
            this.target = base.transform;
        }
        else
        {
            this.target = Object.FindObjectOfType<PlayerController>().transform;
        }
        if (!this.soundOnStart)
        {
            this.currentTimeBetweenSounds = this.NewSoundCooldown();
        }
    }

    // Token: 0x060001D4 RID: 468 RVA: 0x00008EC0 File Offset: 0x000070C0
    public void Update()
    {
        if (this.soundEffects.Length != 0)
        {
            if (this.currentTimeBetweenSounds <= 0f)
            {
                Object.Instantiate<GameObject>(this.soundEffects[Random.Range(0, this.soundEffects.Length)], this.target.position, Quaternion.identity);
                this.currentTimeBetweenSounds = this.NewSoundCooldown();
                return;
            }
            this.currentTimeBetweenSounds -= Time.deltaTime;
        }
    }

    // Token: 0x060001D5 RID: 469 RVA: 0x00008F2D File Offset: 0x0000712D
    public float NewSoundCooldown()
    {
        return Random.Range(this.minSecondsBetweenSounds, this.maxSecondsBetweenSounds);
    }

    // Token: 0x04000243 RID: 579
    public GameObject[] soundEffects;

    // Token: 0x04000244 RID: 580
    public float minSecondsBetweenSounds;

    // Token: 0x04000245 RID: 581
    public float maxSecondsBetweenSounds;

    // Token: 0x04000246 RID: 582
    public float currentTimeBetweenSounds;

    // Token: 0x04000247 RID: 583
    public bool soundOnStart;

    // Token: 0x04000248 RID: 584
    public bool isEnemy;

    // Token: 0x04000249 RID: 585
    private Transform target;
}

using System;
using UnityEngine;

// Token: 0x02000058 RID: 88
public class SoundOnDestroy : MonoBehaviour
{
    // Token: 0x060001CA RID: 458 RVA: 0x00008D88 File Offset: 0x00006F88
    private void OnDestroy()
    {
        Object.Instantiate<GameObject>(this.sound, base.transform.position, Quaternion.identity);
    }

    // Token: 0x0400023D RID: 573
    public GameObject sound;
}

using System;
using UnityEngine;

// Token: 0x0200006E RID: 110
public class SpaceDoor : MonoBehaviour
{
    // Token: 0x06000229 RID: 553 RVA: 0x0000A7EF File Offset: 0x000089EF
    private void Start()
    {
        this._animator = base.GetComponent<Animator>();
    }

    // Token: 0x0600022A RID: 554 RVA: 0x0000A7FD File Offset: 0x000089FD
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && this.itemCollector.items >= this.itemsToEnter)
        {
            this._animator.SetBool("isOpened", true);
        }
    }

    // Token: 0x0600022B RID: 555 RVA: 0x0000A830 File Offset: 0x00008A30
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this._animator.SetBool("isOpened", false);
        }
    }

    // Token: 0x0400029C RID: 668
    [SerializeField]
    private ItemCollector itemCollector;

    // Token: 0x0400029D RID: 669
    [SerializeField]
    private int itemsToEnter;

    // Token: 0x0400029E RID: 670
    private Animator _animator;
}

using System;
using UnityEngine;

// Token: 0x0200006C RID: 108
public class SparksBullet : MonoBehaviour
{
    // Token: 0x06000222 RID: 546 RVA: 0x0000A5FC File Offset: 0x000087FC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            if (other.transform.root.GetComponent<Enemy>() != null)
            {
                other.transform.root.GetComponent<Enemy>().TakeDamage(this.bulletDamage);
                Object.Destroy(base.gameObject);
                return;
            }
            if (other.transform.root.GetComponent<MushroomHunter>() != null)
            {
                other.transform.root.GetComponent<MushroomHunter>().TakeDamage(this.bulletDamage);
                Object.Destroy(base.gameObject);
                return;
            }
            if (other.transform.root.GetComponent<Boss>() != null)
            {
                other.transform.root.GetComponent<Boss>().TakeDamage(this.bulletDamage);
                Object.Destroy(base.gameObject);
                return;
            }
        }
        else
        {
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x06000223 RID: 547 RVA: 0x0000A6E1 File Offset: 0x000088E1
    private void OnDestroy()
    {
        Object.Instantiate<GameObject>(this.fireBall, base.transform.position, Quaternion.identity);
    }

    // Token: 0x04000293 RID: 659
    public int bulletDamage;

    // Token: 0x04000294 RID: 660
    public GameObject fireBall;
}

using System;
using UnityEngine;

// Token: 0x02000073 RID: 115
public class SpawnEffect : MonoBehaviour
{
    // Token: 0x06000238 RID: 568 RVA: 0x0000AA10 File Offset: 0x00008C10
    private void Start()
    {
        this.shaderProperty = Shader.PropertyToID("_cutoff");
        this._renderer = base.GetComponent<Renderer>();
        this.ps = base.GetComponentInChildren<ParticleSystem>();
        this.ps.main.duration = this.spawnEffectTime;
        this.ps.Play();
    }

    // Token: 0x06000239 RID: 569 RVA: 0x0000AA6C File Offset: 0x00008C6C
    private void Update()
    {
        if (this.timer < this.spawnEffectTime + this.pause)
        {
            this.timer += Time.deltaTime;
        }
        else
        {
            this.ps.Play();
            this.timer = 0f;
        }
        this._renderer.material.SetFloat(this.shaderProperty, this.fadeIn.Evaluate(Mathf.InverseLerp(0f, this.spawnEffectTime, this.timer)));
    }

    // Token: 0x040002A8 RID: 680
    public float spawnEffectTime = 2f;

    // Token: 0x040002A9 RID: 681
    public float pause = 1f;

    // Token: 0x040002AA RID: 682
    public AnimationCurve fadeIn;

    // Token: 0x040002AB RID: 683
    private ParticleSystem ps;

    // Token: 0x040002AC RID: 684
    private float timer;

    // Token: 0x040002AD RID: 685
    private Renderer _renderer;

    // Token: 0x040002AE RID: 686
    private int shaderProperty;
}

using System;
using System.Collections.Generic;
using System.Reflection;

// Token: 0x02000078 RID: 120
public static class SubclassFinder
{
    // Token: 0x06000244 RID: 580 RVA: 0x0000AE48 File Offset: 0x00009048
    public static List<Type> GetSubclasses<T>()
    {
        List<Type> list = new List<Type>();
        Type typeFromHandle = typeof(T);
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            foreach (Type type in assemblies[i].GetTypes())
            {
                if (type.IsSubclassOf(typeFromHandle) || type == typeFromHandle)
                {
                    list.Add(type);
                }
            }
        }
        return list;
    }
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x0200006F RID: 111
public class TextChanger : MonoBehaviour
{
    // Token: 0x0600022D RID: 557 RVA: 0x0000A858 File Offset: 0x00008A58
    public void CallNextLine()
    {
        this.textUI.text = this.textLines[this.lineNumber];
    }

    // Token: 0x0400029F RID: 671
    public TextMeshProUGUI textUI;

    // Token: 0x040002A0 RID: 672
    public string[] textLines;

    // Token: 0x040002A1 RID: 673
    public int lineNumber;
}

using System;
using UnityEngine;

// Token: 0x02000072 RID: 114
public class TextRotator : MonoBehaviour
{
    // Token: 0x06000236 RID: 566 RVA: 0x0000A9C8 File Offset: 0x00008BC8
    private void Update()
    {
        Vector3 position = Camera.main.transform.position;
        position.y = base.transform.position.y;
        base.transform.LookAt(position);
    }
}

using System;
using UnityEngine;

// Token: 0x02000056 RID: 86
public class ThrowingTutorial : MonoBehaviour
{
    // Token: 0x060001C1 RID: 449 RVA: 0x00008C21 File Offset: 0x00006E21
    private void Start()
    {
        this.readyToThrow = true;
    }

    // Token: 0x060001C2 RID: 450 RVA: 0x00008C2A File Offset: 0x00006E2A
    private void Update()
    {
        if (this.readyToThrow && this.totalThrows > 0)
        {
            this.Throw();
        }
    }

    // Token: 0x060001C3 RID: 451 RVA: 0x00008C44 File Offset: 0x00006E44
    private void Throw()
    {
        this.readyToThrow = false;
        Rigidbody component = Object.Instantiate<GameObject>(this.objectToThrow, this.attackPoint.position, this.attackPoint.rotation).GetComponent<Rigidbody>();
        Vector3 a = this.attackPoint.transform.forward;
        RaycastHit raycastHit;
        if (Physics.Raycast(this.attackPoint.position, this.attackPoint.forward, out raycastHit, 500f))
        {
            a = (raycastHit.point - this.attackPoint.position).normalized;
        }
        Vector3 force = a * this.throwForce + base.transform.up * this.throwUpwardForce;
        component.AddForce(force, ForceMode.Impulse);
        this.totalThrows--;
        base.Invoke("ResetThrow", this.throwCooldown);
    }

    // Token: 0x060001C4 RID: 452 RVA: 0x00008D21 File Offset: 0x00006F21
    private void ResetThrow()
    {
        this.readyToThrow = true;
    }

    // Token: 0x04000232 RID: 562
    [Header("References")]
    public Transform attackPoint;

    // Token: 0x04000233 RID: 563
    public GameObject objectToThrow;

    // Token: 0x04000234 RID: 564
    [Header("Settings")]
    public int totalThrows;

    // Token: 0x04000235 RID: 565
    public float throwCooldown;

    // Token: 0x04000236 RID: 566
    [Header("Throwing")]
    public KeyCode throwKey = KeyCode.Mouse0;

    // Token: 0x04000237 RID: 567
    public float throwForce;

    // Token: 0x04000238 RID: 568
    public float throwUpwardForce;

    // Token: 0x04000239 RID: 569
    private bool readyToThrow;
}

using System;
using UnityEngine;
using UnityEngine.Events;

// Token: 0x02000076 RID: 118
public class TriggerArea : MonoBehaviour
{
    // Token: 0x06000241 RID: 577 RVA: 0x0000AE23 File Offset: 0x00009023
    private void OnTriggerEnter(Collider other)
    {
        this.OnEnter.Invoke();
    }

    // Token: 0x06000242 RID: 578 RVA: 0x0000AE30 File Offset: 0x00009030
    private void OnTriggerExit(Collider other)
    {
        this.OnExit.Invoke();
    }

    // Token: 0x040002BE RID: 702
    public UnityEvent OnEnter;

    // Token: 0x040002BF RID: 703
    public UnityEvent OnExit;
}

using System;
using UnityEngine;

// Token: 0x02000042 RID: 66
public class TrollBullet : MonoBehaviour
{
    // Token: 0x06000161 RID: 353 RVA: 0x00006E6D File Offset: 0x0000506D
    private void Start()
    {
        this.direction = Vector3.forward;
        this.sable = GameObject.FindGameObjectWithTag("Sable").transform.parent.GetComponent<Animator>();
    }

    // Token: 0x06000162 RID: 354 RVA: 0x00006E99 File Offset: 0x00005099
    private void Update()
    {
        base.transform.Translate(this.direction * this.speed * Time.deltaTime);
    }

    // Token: 0x06000163 RID: 355 RVA: 0x00006EC4 File Offset: 0x000050C4
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.GetComponent<PlayerHealth>() != null)
        {
            other.transform.root.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            Object.Destroy(base.gameObject);
            return;
        }
        if (other.CompareTag("Sable") && !this.sable.GetCurrentAnimatorStateInfo(0).IsName("idle"))
        {
            this.direction = -this.direction;
            return;
        }
        if (other.transform.root.name == "EnemyTroll")
        {
            other.transform.root.GetComponent<Enemy>().TakeDamage(this.damageForEnemy);
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x06000164 RID: 356 RVA: 0x00006F8D File Offset: 0x0000518D
    private void OnDestroy()
    {
        Object.Instantiate<GameObject>(this.fireBall, base.transform.position, Quaternion.identity);
    }

    // Token: 0x040001A0 RID: 416
    public int damage;

    // Token: 0x040001A1 RID: 417
    public int damageForEnemy;

    // Token: 0x040001A2 RID: 418
    public float speed;

    // Token: 0x040001A3 RID: 419
    public GameObject fireBall;

    // Token: 0x040001A4 RID: 420
    public Animator sable;

    // Token: 0x040001A5 RID: 421
    private Vector3 direction;
}

using System;
using UnityEngine;

// Token: 0x0200005C RID: 92
public class TrollThrower : MonoBehaviour
{
    // Token: 0x060001D7 RID: 471 RVA: 0x00008F48 File Offset: 0x00007148
    private void Start()
    {
        this.bulletTime = (float)this.startBulletTime;
    }

    // Token: 0x060001D8 RID: 472 RVA: 0x00008F57 File Offset: 0x00007157
    private void Update()
    {
        this.bulletTime -= Time.deltaTime;
        if (this.bulletTime <= 0f)
        {
            this.SpawnSparks();
            this.bulletTime = (float)this.startBulletTime;
        }
    }

    // Token: 0x060001D9 RID: 473 RVA: 0x00008F8C File Offset: 0x0000718C
    public void SpawnSparks()
    {
        this.bulletPos.LookAt(this.player);
        Object.Instantiate<GameObject>(this.bullet, this.bulletPos.position, this.bulletPos.rotation);
        Object.Instantiate<GameObject>(this.sounds[Random.Range(0, this.sounds.Length)], this.bulletPos.position, Quaternion.identity);
    }

    // Token: 0x0400024A RID: 586
    public GameObject bullet;

    // Token: 0x0400024B RID: 587
    public Transform bulletPos;

    // Token: 0x0400024C RID: 588
    public GameObject[] sounds;

    // Token: 0x0400024D RID: 589
    public Transform player;

    // Token: 0x0400024E RID: 590
    public int startBulletTime;

    // Token: 0x0400024F RID: 591
    private float bulletTime;
}

using System;
using UnityEngine;

// Token: 0x02000043 RID: 67
public class Trotil : MonoBehaviour
{
    // Token: 0x06000166 RID: 358 RVA: 0x00006FB3 File Offset: 0x000051B3
    private void Start()
    {
        base.GetComponent<Rigidbody>().AddForce(this.startPush + base.transform.forward * 8f, ForceMode.Impulse);
    }

    // Token: 0x06000167 RID: 359 RVA: 0x00006FE1 File Offset: 0x000051E1
    public void OnExploded()
    {
        Object.Instantiate<GameObject>(this.explosion, base.transform.position, Quaternion.identity);
        Object.Destroy(base.transform.parent.gameObject);
    }

    // Token: 0x040001A6 RID: 422
    public GameObject explosion;

    // Token: 0x040001A7 RID: 423
    public Vector3 startPush;
}

using System;
using UnityEngine;
using UnityEngine.Video;

// Token: 0x0200005D RID: 93
public class TVBehaviour : MonoBehaviour
{
    // Token: 0x060001DB RID: 475 RVA: 0x00008FFF File Offset: 0x000071FF
    private void Start()
    {
        this.spriteRenderer = base.GetComponent<SpriteRenderer>();
    }

    // Token: 0x060001DC RID: 476 RVA: 0x0000900D File Offset: 0x0000720D
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.spriteRenderer.color = Color.white;
            this.clip.Play();
        }
    }

    // Token: 0x060001DD RID: 477 RVA: 0x00009037 File Offset: 0x00007237
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.spriteRenderer.color = Color.black;
            this.clip.Stop();
        }
    }

    // Token: 0x04000250 RID: 592
    private SpriteRenderer spriteRenderer;

    // Token: 0x04000251 RID: 593
    public VideoPlayer clip;
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Token: 0x02000014 RID: 20
public class VariableJoystick : Joystick
{
    // Token: 0x1700000A RID: 10
    // (get) Token: 0x06000055 RID: 85 RVA: 0x00002E78 File Offset: 0x00001078
    // (set) Token: 0x06000056 RID: 86 RVA: 0x00002E80 File Offset: 0x00001080
    public float MoveThreshold
    {
        get
        {
            return this.moveThreshold;
        }
        set
        {
            this.moveThreshold = Mathf.Abs(value);
        }
    }

    // Token: 0x06000057 RID: 87 RVA: 0x00002E8E File Offset: 0x0000108E
    public void SetMode(JoystickType joystickType)
    {
        this.joystickType = joystickType;
        if (joystickType == JoystickType.Fixed)
        {
            this.background.anchoredPosition = this.fixedPosition;
            this.background.gameObject.SetActive(true);
            return;
        }
        this.background.gameObject.SetActive(false);
    }

    // Token: 0x06000058 RID: 88 RVA: 0x00002ECE File Offset: 0x000010CE
    protected override void Start()
    {
        base.Start();
        this.fixedPosition = this.background.anchoredPosition;
        this.SetMode(this.joystickType);
    }

    // Token: 0x06000059 RID: 89 RVA: 0x00002EF3 File Offset: 0x000010F3
    public override void OnPointerDown(PointerEventData eventData)
    {
        if (this.joystickType != JoystickType.Fixed)
        {
            this.background.anchoredPosition = base.ScreenPointToAnchoredPosition(eventData.position);
            this.background.gameObject.SetActive(true);
        }
        base.OnPointerDown(eventData);
    }

    // Token: 0x0600005A RID: 90 RVA: 0x00002F2C File Offset: 0x0000112C
    public override void OnPointerUp(PointerEventData eventData)
    {
        if (this.joystickType != JoystickType.Fixed)
        {
            this.background.gameObject.SetActive(false);
        }
        base.OnPointerUp(eventData);
    }

    // Token: 0x0600005B RID: 91 RVA: 0x00002F50 File Offset: 0x00001150
    protected override void HandleInput(float magnitude, Vector2 normalised, Vector2 radius, Camera cam)
    {
        if (this.joystickType == JoystickType.Dynamic && magnitude > this.moveThreshold)
        {
            Vector2 b = normalised * (magnitude - this.moveThreshold) * radius;
            this.background.anchoredPosition += b;
        }
        base.HandleInput(magnitude, normalised, radius, cam);
    }

    // Token: 0x0400003D RID: 61
    [SerializeField]
    private float moveThreshold = 1f;

    // Token: 0x0400003E RID: 62
    [SerializeField]
    private JoystickType joystickType;

    // Token: 0x0400003F RID: 63
    private Vector2 fixedPosition = Vector2.zero;
}

using System;
using Ditzelgames;
using UnityEngine;

// Token: 0x02000044 RID: 68
[RequireComponent(typeof(WaterFloat))]
public class WaterBoat : MonoBehaviour
{
    // Token: 0x06000169 RID: 361 RVA: 0x0000701C File Offset: 0x0000521C
    public void Awake()
    {
        this.ParticleSystem = base.GetComponentInChildren<ParticleSystem>();
        this.Rigidbody = base.GetComponent<Rigidbody>();
        this.StartRotation = this.Motor.localRotation;
        this.Camera = Camera.main;
    }

    // Token: 0x0600016A RID: 362 RVA: 0x00007054 File Offset: 0x00005254
    public void FixedUpdate()
    {
        Vector3 forward = base.transform.forward;
        int num = 0;
        if (Input.GetKey(KeyCode.A))
        {
            num = 1;
        }
        if (Input.GetKey(KeyCode.D))
        {
            num = -1;
        }
        this.Rigidbody.AddForceAtPosition((float)num * base.transform.right * this.SteerPower / 100f, this.Motor.position);
        Vector3 a = Vector3.Scale(new Vector3(1f, 0f, 1f), base.transform.forward);
        Vector3 zero = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
        {
            PhysicsHelper.ApplyForceToReachVelocity(this.Rigidbody, a * this.MaxSpeed, this.Power, ForceMode.Force);
        }
        if (Input.GetKey(KeyCode.S))
        {
            PhysicsHelper.ApplyForceToReachVelocity(this.Rigidbody, a * -this.MaxSpeed, this.Power, ForceMode.Force);
        }
        this.Motor.SetPositionAndRotation(this.Motor.position, base.transform.rotation * this.StartRotation * Quaternion.Euler(0f, 30f * (float)num, 0f));
        if (this.ParticleSystem != null)
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S))
            {
                this.ParticleSystem.Play();
            }
            else
            {
                this.ParticleSystem.Pause();
            }
        }
        bool flag = Vector3.Cross(base.transform.forward, this.Rigidbody.velocity).y < 0f;
        this.Rigidbody.velocity = Quaternion.AngleAxis(Vector3.SignedAngle(this.Rigidbody.velocity, (flag ? 1f : 0f) * base.transform.forward, Vector3.up) * this.Drag, Vector3.up) * this.Rigidbody.velocity;
    }

    // Token: 0x040001A8 RID: 424
    public Transform Motor;

    // Token: 0x040001A9 RID: 425
    public float SteerPower = 500f;

    // Token: 0x040001AA RID: 426
    public float Power = 5f;

    // Token: 0x040001AB RID: 427
    public float MaxSpeed = 10f;

    // Token: 0x040001AC RID: 428
    public float Drag = 0.1f;

    // Token: 0x040001AD RID: 429
    protected Rigidbody Rigidbody;

    // Token: 0x040001AE RID: 430
    protected Quaternion StartRotation;

    // Token: 0x040001AF RID: 431
    protected ParticleSystem ParticleSystem;

    // Token: 0x040001B0 RID: 432
    protected Camera Camera;

    // Token: 0x040001B1 RID: 433
    protected Vector3 CamVel;
}

using System;
using Ditzelgames;
using UnityEngine;

// Token: 0x02000065 RID: 101
[RequireComponent(typeof(Rigidbody))]
public class WaterFloat : MonoBehaviour
{
    // Token: 0x17000010 RID: 16
    // (get) Token: 0x060001FE RID: 510 RVA: 0x0000945A File Offset: 0x0000765A
    public Vector3 Center
    {
        get
        {
            return base.transform.position + this.centerOffset;
        }
    }

    // Token: 0x060001FF RID: 511 RVA: 0x00009474 File Offset: 0x00007674
    private void Awake()
    {
        this.Waves = Object.FindObjectOfType<Waves>();
        this.Rigidbody = base.GetComponent<Rigidbody>();
        this.Rigidbody.useGravity = false;
        this.WaterLinePoints = new Vector3[this.FloatPoints.Length];
        for (int i = 0; i < this.FloatPoints.Length; i++)
        {
            this.WaterLinePoints[i] = this.FloatPoints[i].position;
        }
        this.centerOffset = PhysicsHelper.GetCenter(this.WaterLinePoints) - base.transform.position;
    }

    // Token: 0x06000200 RID: 512 RVA: 0x00009504 File Offset: 0x00007704
    private void FixedUpdate()
    {
        float num = 0f;
        bool flag = false;
        for (int i = 0; i < this.FloatPoints.Length; i++)
        {
            this.WaterLinePoints[i] = this.FloatPoints[i].position;
            this.WaterLinePoints[i].y = this.Waves.GetHeight(this.FloatPoints[i].position);
            num += this.WaterLinePoints[i].y / (float)this.FloatPoints.Length;
            if (this.WaterLinePoints[i].y > this.FloatPoints[i].position.y)
            {
                flag = true;
            }
        }
        float d = num - this.WaterLine;
        this.WaterLine = num;
        this.TargetUp = PhysicsHelper.GetNormal(this.WaterLinePoints);
        Vector3 a = Physics.gravity;
        this.Rigidbody.drag = this.AirDrag;
        if (this.WaterLine > this.Center.y)
        {
            this.Rigidbody.drag = this.WaterDrag;
            if (this.AttachToSurface)
            {
                this.Rigidbody.position = new Vector3(this.Rigidbody.position.x, this.WaterLine - this.centerOffset.y, this.Rigidbody.position.z);
            }
            else
            {
                a = (this.AffectDirection ? (this.TargetUp * -Physics.gravity.y) : (-Physics.gravity));
                base.transform.Translate(Vector3.up * d * 0.9f);
            }
        }
        this.Rigidbody.AddForce(a * Mathf.Clamp(Mathf.Abs(this.WaterLine - this.Center.y), 0f, 1f));
        if (flag)
        {
            this.TargetUp = Vector3.SmoothDamp(base.transform.up, this.TargetUp, ref this.smoothVectorRotation, 0.2f);
            this.Rigidbody.rotation = Quaternion.FromToRotation(base.transform.up, this.TargetUp) * this.Rigidbody.rotation;
        }
    }

    // Token: 0x06000201 RID: 513 RVA: 0x0000974C File Offset: 0x0000794C
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        if (this.FloatPoints == null)
        {
            return;
        }
        for (int i = 0; i < this.FloatPoints.Length; i++)
        {
            if (!(this.FloatPoints[i] == null))
            {
                if (this.Waves != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawCube(this.WaterLinePoints[i], Vector3.one * 0.3f);
                }
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(this.FloatPoints[i].position, 0.1f);
            }
        }
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawCube(new Vector3(this.Center.x, this.WaterLine, this.Center.z), Vector3.one * 1f);
            Gizmos.DrawRay(new Vector3(this.Center.x, this.WaterLine, this.Center.z), this.TargetUp * 1f);
        }
    }

    // Token: 0x04000265 RID: 613
    public float AirDrag = 1f;

    // Token: 0x04000266 RID: 614
    public float WaterDrag = 10f;

    // Token: 0x04000267 RID: 615
    public bool AffectDirection = true;

    // Token: 0x04000268 RID: 616
    public bool AttachToSurface;

    // Token: 0x04000269 RID: 617
    public Transform[] FloatPoints;

    // Token: 0x0400026A RID: 618
    protected Rigidbody Rigidbody;

    // Token: 0x0400026B RID: 619
    protected Waves Waves;

    // Token: 0x0400026C RID: 620
    protected float WaterLine;

    // Token: 0x0400026D RID: 621
    protected Vector3[] WaterLinePoints;

    // Token: 0x0400026E RID: 622
    protected Vector3 smoothVectorRotation;

    // Token: 0x0400026F RID: 623
    protected Vector3 TargetUp;

    // Token: 0x04000270 RID: 624
    protected Vector3 centerOffset;
}

using System;
using UnityEngine;

// Token: 0x02000066 RID: 102
public class Waves : MonoBehaviour
{
    // Token: 0x06000203 RID: 515 RVA: 0x0000988C File Offset: 0x00007A8C
    private void Start()
    {
        this.Mesh = new Mesh();
        this.Mesh.name = base.gameObject.name;
        this.Mesh.vertices = this.GenerateVerts();
        this.Mesh.triangles = this.GenerateTries();
        this.Mesh.uv = this.GenerateUVs();
        this.Mesh.RecalculateNormals();
        this.Mesh.RecalculateBounds();
        this.MeshFilter = base.gameObject.AddComponent<MeshFilter>();
        this.MeshFilter.mesh = this.Mesh;
    }

    // Token: 0x06000204 RID: 516 RVA: 0x00009928 File Offset: 0x00007B28
    public float GetHeight(Vector3 position)
    {
        Vector3 b = new Vector3(1f / base.transform.lossyScale.x, 0f, 1f / base.transform.lossyScale.z);
        Vector3 vector = Vector3.Scale(position - base.transform.position, b);
        Vector3 vector2 = new Vector3(Mathf.Floor(vector.x), 0f, Mathf.Floor(vector.z));
        Vector3 vector3 = new Vector3(Mathf.Floor(vector.x), 0f, Mathf.Ceil(vector.z));
        Vector3 vector4 = new Vector3(Mathf.Ceil(vector.x), 0f, Mathf.Floor(vector.z));
        Vector3 vector5 = new Vector3(Mathf.Ceil(vector.x), 0f, Mathf.Ceil(vector.z));
        vector2.x = Mathf.Clamp(vector2.x, 0f, (float)this.Dimension);
        vector2.z = Mathf.Clamp(vector2.z, 0f, (float)this.Dimension);
        vector3.x = Mathf.Clamp(vector3.x, 0f, (float)this.Dimension);
        vector3.z = Mathf.Clamp(vector3.z, 0f, (float)this.Dimension);
        vector4.x = Mathf.Clamp(vector4.x, 0f, (float)this.Dimension);
        vector4.z = Mathf.Clamp(vector4.z, 0f, (float)this.Dimension);
        vector5.x = Mathf.Clamp(vector5.x, 0f, (float)this.Dimension);
        vector5.z = Mathf.Clamp(vector5.z, 0f, (float)this.Dimension);
        float num = Mathf.Max(new float[]
        {
            Vector3.Distance(vector2, vector),
            Vector3.Distance(vector3, vector),
            Vector3.Distance(vector4, vector),
            Vector3.Distance(vector5, vector) + Mathf.Epsilon
        });
        float num2 = num - Vector3.Distance(vector2, vector) + (num - Vector3.Distance(vector3, vector)) + (num - Vector3.Distance(vector4, vector)) + (num - Vector3.Distance(vector5, vector) + Mathf.Epsilon);
        return (this.Mesh.vertices[this.index(vector2.x, vector2.z)].y * (num - Vector3.Distance(vector2, vector)) + this.Mesh.vertices[this.index(vector3.x, vector3.z)].y * (num - Vector3.Distance(vector3, vector)) + this.Mesh.vertices[this.index(vector4.x, vector4.z)].y * (num - Vector3.Distance(vector4, vector)) + this.Mesh.vertices[this.index(vector5.x, vector5.z)].y * (num - Vector3.Distance(vector5, vector))) * base.transform.lossyScale.y / num2;
    }

    // Token: 0x06000205 RID: 517 RVA: 0x00009C58 File Offset: 0x00007E58
    private Vector3[] GenerateVerts()
    {
        Vector3[] array = new Vector3[(this.Dimension + 1) * (this.Dimension + 1)];
        for (int i = 0; i <= this.Dimension; i++)
        {
            for (int j = 0; j <= this.Dimension; j++)
            {
                array[this.index(i, j)] = new Vector3((float)i, 0f, (float)j);
            }
        }
        return array;
    }

    // Token: 0x06000206 RID: 518 RVA: 0x00009CBC File Offset: 0x00007EBC
    private int[] GenerateTries()
    {
        int[] array = new int[this.Mesh.vertices.Length * 6];
        for (int i = 0; i < this.Dimension; i++)
        {
            for (int j = 0; j < this.Dimension; j++)
            {
                array[this.index(i, j) * 6] = this.index(i, j);
                array[this.index(i, j) * 6 + 1] = this.index(i + 1, j + 1);
                array[this.index(i, j) * 6 + 2] = this.index(i + 1, j);
                array[this.index(i, j) * 6 + 3] = this.index(i, j);
                array[this.index(i, j) * 6 + 4] = this.index(i, j + 1);
                array[this.index(i, j) * 6 + 5] = this.index(i + 1, j + 1);
            }
        }
        return array;
    }

    // Token: 0x06000207 RID: 519 RVA: 0x00009D9C File Offset: 0x00007F9C
    private Vector2[] GenerateUVs()
    {
        Vector2[] array = new Vector2[this.Mesh.vertices.Length];
        for (int i = 0; i <= this.Dimension; i++)
        {
            for (int j = 0; j <= this.Dimension; j++)
            {
                Vector2 vector = new Vector2((float)i / this.UVScale % 2f, (float)j / this.UVScale % 2f);
                array[this.index(i, j)] = new Vector2((vector.x <= 1f) ? vector.x : (2f - vector.x), (vector.y <= 1f) ? vector.y : (2f - vector.y));
            }
        }
        return array;
    }

    // Token: 0x06000208 RID: 520 RVA: 0x00009E62 File Offset: 0x00008062
    private int index(int x, int z)
    {
        return x * (this.Dimension + 1) + z;
    }

    // Token: 0x06000209 RID: 521 RVA: 0x00009E70 File Offset: 0x00008070
    private int index(float x, float z)
    {
        return this.index((int)x, (int)z);
    }

    // Token: 0x0600020A RID: 522 RVA: 0x00009E7C File Offset: 0x0000807C
    private void Update()
    {
        Vector3[] vertices = this.Mesh.vertices;
        for (int i = 0; i <= this.Dimension; i++)
        {
            for (int j = 0; j <= this.Dimension; j++)
            {
                float num = 0f;
                for (int k = 0; k < this.Octaves.Length; k++)
                {
                    if (this.Octaves[k].alternate)
                    {
                        float num2 = Mathf.PerlinNoise((float)i * this.Octaves[k].scale.x / (float)this.Dimension, (float)j * this.Octaves[k].scale.y / (float)this.Dimension) * 3.1415927f * 2f;
                        num += Mathf.Cos(num2 + this.Octaves[k].speed.magnitude * Time.time) * this.Octaves[k].height;
                    }
                    else
                    {
                        float num3 = Mathf.PerlinNoise(((float)i * this.Octaves[k].scale.x + Time.time * this.Octaves[k].speed.x) / (float)this.Dimension, ((float)j * this.Octaves[k].scale.y + Time.time * this.Octaves[k].speed.y) / (float)this.Dimension) - 0.5f;
                        num += num3 * this.Octaves[k].height;
                    }
                }
                vertices[this.index(i, j)] = new Vector3((float)i, num, (float)j);
            }
        }
        this.Mesh.vertices = vertices;
        this.Mesh.RecalculateNormals();
    }

    // Token: 0x04000271 RID: 625
    public int Dimension = 10;

    // Token: 0x04000272 RID: 626
    public float UVScale = 2f;

    // Token: 0x04000273 RID: 627
    public Waves.Octave[] Octaves;

    // Token: 0x04000274 RID: 628
    protected MeshFilter MeshFilter;

    // Token: 0x04000275 RID: 629
    protected Mesh Mesh;

    // Token: 0x020000D3 RID: 211
    [Serializable]
    public struct Octave
    {
        // Token: 0x0400048A RID: 1162
        public Vector2 speed;

        // Token: 0x0400048B RID: 1163
        public Vector2 scale;

        // Token: 0x0400048C RID: 1164
        public float height;

        // Token: 0x0400048D RID: 1165
        public bool alternate;
    }
}

using System;
using UnityEngine;

// Token: 0x0200006D RID: 109
public class WeaponController : MonoBehaviour
{
    // Token: 0x06000225 RID: 549 RVA: 0x0000A707 File Offset: 0x00008907
    private void Start()
    {
        this.weaponAnimator = base.GetComponent<Animator>();
    }

    // Token: 0x06000226 RID: 550 RVA: 0x0000A718 File Offset: 0x00008918
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            this.weaponAnimator.SetTrigger("isAttacking");
        }
        if (this.newSparks != null)
        {
            this.newSparks.transform.Translate(this.sparksDirection * (float)this.sparksSpeed * Time.deltaTime);
        }
    }

    // Token: 0x06000227 RID: 551 RVA: 0x0000A778 File Offset: 0x00008978
    public void SpawnSparks()
    {
        this.newSparks = Object.Instantiate<GameObject>(this.sparksEffect, this.sparksPos.position, Quaternion.identity);
        this.sparksDirection = this.sparksPos.transform.forward;
        Object.Instantiate<GameObject>(this.sounds[Random.Range(0, this.sounds.Length)], this.sparksPos.position, Quaternion.identity);
    }

    // Token: 0x04000295 RID: 661
    public GameObject sparksEffect;

    // Token: 0x04000296 RID: 662
    public GameObject newSparks;

    // Token: 0x04000297 RID: 663
    public Transform sparksPos;

    // Token: 0x04000298 RID: 664
    public int sparksSpeed;

    // Token: 0x04000299 RID: 665
    public GameObject[] sounds;

    // Token: 0x0400029A RID: 666
    private Animator weaponAnimator;

    // Token: 0x0400029B RID: 667
    private Vector3 sparksDirection;
}

using System;
using UnityEngine;

// Token: 0x02000064 RID: 100
public class YT : MonoBehaviour
{
    // Token: 0x060001FB RID: 507 RVA: 0x0000943A File Offset: 0x0000763A
    public void OnButtonDown()
    {
        Application.OpenURL("https://www.youtube.com/channel/UCmjNGJn0WNPJS_G52UfkH-g");
    }

    // Token: 0x060001FC RID: 508 RVA: 0x00009446 File Offset: 0x00007646
    public void OnSteamButtonDown()
    {
        Application.OpenURL("https://store.steampowered.com/app/2477600/Offscreen/");
    }
}


// Тайна мемного королевства

using System;
using UnityEngine;

// Token: 0x0200000F RID: 15
public class AfterTitles : MonoBehaviour
{
    // Token: 0x06000069 RID: 105 RVA: 0x00003BA4 File Offset: 0x00001DA4
    public void OnTitlesOver()
    {
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        if (this.spawner != null)
        {
            Object.Destroy(this.spawner);
        }
        PlayerPrefs.DeleteAll();
        CursorController.UnlockCursor();
        this.menu.OnExit();
    }

    // Token: 0x04000094 RID: 148
    public MenuExit menu;

    // Token: 0x04000095 RID: 149
    public GameObject spawner;
}

using System;

// Token: 0x02000089 RID: 137
[Serializable]
public enum AvatarPosition
{
    // Token: 0x04000386 RID: 902
    None,
    // Token: 0x04000387 RID: 903
    Left,
    // Token: 0x04000388 RID: 904
    Right
}

using System;

// Token: 0x0200008A RID: 138
[Serializable]
public enum AvatarType
{
    // Token: 0x0400038A RID: 906
    Normal,
    // Token: 0x0400038B RID: 907
    Smile,
    // Token: 0x0400038C RID: 908
    Suprized,
    // Token: 0x0400038D RID: 909
    Disgust,
    // Token: 0x0400038E RID: 910
    Crying,
    // Token: 0x0400038F RID: 911
    Angry
}

using System;

// Token: 0x0200007E RID: 126
public enum AxisOptions
{
    // Token: 0x04000366 RID: 870
    Both,
    // Token: 0x04000367 RID: 871
    Horizontal,
    // Token: 0x04000368 RID: 872
    Vertical
}

using System;
using System.Collections;
using UnityEngine;

// Token: 0x0200004C RID: 76
public class BarrierDamageDealer : MonoBehaviour
{
    // Token: 0x06000187 RID: 391 RVA: 0x00007FC8 File Offset: 0x000061C8
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.CompareTag("Player"))
        {
            this.playerInArea = true;
            this.warningText.SetActive(true);
            if (this.damageCoroutine == null)
            {
                this.damageCoroutine = base.StartCoroutine(this.ApplyDamage(other.transform.root.GetComponent<PlayerHealth>()));
            }
        }
    }

    // Token: 0x06000188 RID: 392 RVA: 0x0000802C File Offset: 0x0000622C
    private void OnTriggerExit(Collider other)
    {
        if (other.transform.root.CompareTag("Player"))
        {
            this.playerInArea = false;
            this.warningText.SetActive(false);
            if (this.damageCoroutine != null)
            {
                base.StopCoroutine(this.damageCoroutine);
                this.damageCoroutine = null;
            }
        }
    }

    // Token: 0x06000189 RID: 393 RVA: 0x0000807E File Offset: 0x0000627E
    private IEnumerator ApplyDamage(PlayerHealth player)
    {
        while (this.playerInArea)
        {
            if (player != null)
            {
                player.TakeDamage((float)this.damagePerSecond);
            }
            yield return new WaitForSeconds(this.damageInterval);
        }
        this.damageCoroutine = null;
        yield break;
    }

    // Token: 0x04000209 RID: 521
    public GameObject warningText;

    // Token: 0x0400020A RID: 522
    public int damagePerSecond = 10;

    // Token: 0x0400020B RID: 523
    private float damageInterval = 1f;

    // Token: 0x0400020C RID: 524
    private bool playerInArea;

    // Token: 0x0400020D RID: 525
    private Coroutine damageCoroutine;
}

using System;
using UnityEngine;

// Token: 0x02000007 RID: 7
public class BasicEnemy : MonoBehaviour
{
    // Token: 0x0600000E RID: 14 RVA: 0x000021FD File Offset: 0x000003FD
    public void TakeDamage(int damage)
    {
        this.health -= damage;
        if (this.health <= 0)
        {
            this.DestroyEnemy();
        }
    }

    // Token: 0x0600000F RID: 15 RVA: 0x0000221C File Offset: 0x0000041C
    public void DestroyEnemy()
    {
        Object.Destroy(base.gameObject);
    }

    // Token: 0x0400000E RID: 14
    [Header("Stats")]
    public int health;
}

using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// Token: 0x0200001F RID: 31
public class BeastBehaviour : MonoBehaviour
{
    // Token: 0x060000B7 RID: 183 RVA: 0x00004DFD File Offset: 0x00002FFD
    private void Start()
    {
        this.animator = base.GetComponent<Animator>();
        this.agent = base.GetComponent<NavMeshAgent>();
        this.sphereCollider = base.GetComponent<SphereCollider>();
    }

    // Token: 0x060000B8 RID: 184 RVA: 0x00004E23 File Offset: 0x00003023
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.OnEnterArea();
        }
    }

    // Token: 0x060000B9 RID: 185 RVA: 0x00004E38 File Offset: 0x00003038
    public void OnEnterArea()
    {
        if (this.firstTime)
        {
            this.sphereCollider.enabled = false;
            this.firstTime = false;
        }
        this.dialogues[this.dialogueNum].Invoke();
        if (this.dialogueNum == 1)
        {
            GameObject[] array = GameObject.FindGameObjectsWithTag("Trotil");
            for (int i = 0; i < array.Length; i++)
            {
                Object.Destroy(array[i]);
            }
        }
        this.isCheckingEnd = true;
    }

    // Token: 0x060000BA RID: 186 RVA: 0x00004EA3 File Offset: 0x000030A3
    private void Update()
    {
        if (this.isCheckingEnd)
        {
            this.CheckDialogueEnd();
        }
    }

    // Token: 0x060000BB RID: 187 RVA: 0x00004EB4 File Offset: 0x000030B4
    public void CheckDialogueEnd()
    {
        if (!this.UI.activeInHierarchy)
        {
            this.isCheckingEnd = false;
            if (this.dialogueNum == 0)
            {
                base.Invoke("StartFight", 0.8f);
            }
            if (this.dialogueNum == 1)
            {
                base.Invoke("DisappearCompletely", 0.8f);
            }
        }
    }

    // Token: 0x060000BC RID: 188 RVA: 0x00004F06 File Offset: 0x00003106
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.skip.Invoke();
        }
    }

    // Token: 0x060000BD RID: 189 RVA: 0x00004F20 File Offset: 0x00003120
    private void StartFight()
    {
        this.pier1.presence = "true";
        this.pier2.presence = "true";
        this.pier1.SavePresence();
        this.pier2.SavePresence();
        this.animator.SetBool("hasIncreased", true);
        this.agent.speed = 3.3f;
        this.trotilSpawner.SetActive(true);
        this.afterLab.SetActive(true);
        this.barrier.SetActive(false);
        Object.FindObjectOfType<MusicManager>();
    }

    // Token: 0x060000BE RID: 190 RVA: 0x00004FB0 File Offset: 0x000031B0
    public void OnBoatEntered()
    {
        this.agent.enabled = false;
        this.sphereCollider.enabled = true;
        this.trap.SetActive(true);
        this.dialogueNum = 1;
        this.pier1.presence = "false";
        this.pier1.SavePresence();
        base.Invoke("ActivateTip", 1.7f);
        this.animator.SetBool("hasIncreased", false);
        this.agent.speed = 0f;
        Object.Instantiate<GameObject>(this.smoke, base.transform.position, Quaternion.identity);
        base.transform.position = this.finalPos.position;
        base.transform.rotation = this.finalPos.rotation;
        this.agent.enabled = true;
    }

    // Token: 0x060000BF RID: 191 RVA: 0x00005088 File Offset: 0x00003288
    public void ActivateTip()
    {
        if (this.tip != null)
        {
            this.tip.SetActive(true);
        }
    }

    // Token: 0x060000C0 RID: 192 RVA: 0x000050A4 File Offset: 0x000032A4
    public void DisappearCompletely()
    {
        Object.FindObjectOfType<MusicManager>();
        Object.Instantiate<GameObject>(this.smoke, base.transform.position, Quaternion.identity);
        this.flowerPile.SetActive(true);
        this.trap.SetActive(false);
        this.pier2.presence = "false";
        this.pier2.SavePresence();
        Object.Destroy(base.gameObject);
    }

    // Token: 0x04000100 RID: 256
    public bool firstTime;

    // Token: 0x04000101 RID: 257
    public bool isCheckingEnd;

    // Token: 0x04000102 RID: 258
    public GameObject UI;

    // Token: 0x04000103 RID: 259
    public GameObject trotilSpawner;

    // Token: 0x04000104 RID: 260
    public UnityEvent[] dialogues;

    // Token: 0x04000105 RID: 261
    public UnityEvent skip;

    // Token: 0x04000106 RID: 262
    public GameObject afterLab;

    // Token: 0x04000107 RID: 263
    public GameObject barrier;

    // Token: 0x04000108 RID: 264
    public GameObject tip;

    // Token: 0x04000109 RID: 265
    public GameObject smoke;

    // Token: 0x0400010A RID: 266
    public GameObject trap;

    // Token: 0x0400010B RID: 267
    public Pier pier1;

    // Token: 0x0400010C RID: 268
    public Pier pier2;

    // Token: 0x0400010D RID: 269
    public GameObject flowerPile;

    // Token: 0x0400010E RID: 270
    public Transform finalPos;

    // Token: 0x0400010F RID: 271
    public int dialogueNum;

    // Token: 0x04000110 RID: 272
    private Animator animator;

    // Token: 0x04000111 RID: 273
    private NavMeshAgent agent;

    // Token: 0x04000112 RID: 274
    private SphereCollider sphereCollider;
}

using System;
using Ditzelgames;
using UnityEngine;

// Token: 0x02000020 RID: 32
public class BoatController : MonoBehaviour
{
    // Token: 0x060000C2 RID: 194 RVA: 0x00005119 File Offset: 0x00003319
    private void Start()
    {
        this.ParticleSystem = base.GetComponentInChildren<ParticleSystem>();
        this.rb = base.GetComponent<Rigidbody>();
        this.StartRotation = this.motor.localRotation;
    }

    // Token: 0x060000C3 RID: 195 RVA: 0x00005144 File Offset: 0x00003344
    private void FixedUpdate()
    {
        this.MoveBoat();
    }

    // Token: 0x060000C4 RID: 196 RVA: 0x0000514C File Offset: 0x0000334C
    private void MoveBoat()
    {
        Vector3 forward = base.transform.forward;
        int num = 0;
        if (Input.GetKey(KeyCode.A))
        {
            num = -1;
        }
        if (Input.GetKey(KeyCode.D))
        {
            num = 1;
        }
        this.rb.AddForceAtPosition((float)num * base.transform.right * this.steerPower / 100f, this.motor.position);
        Vector3 a = Vector3.Scale(new Vector3(1f, 0f, 1f), this.orientation.forward);
        Vector3 zero = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
        {
            PhysicsHelper.ApplyForceToReachVelocity(this.rb, a * this.speed, this.power, ForceMode.Force);
        }
        if (Input.GetKey(KeyCode.S))
        {
            PhysicsHelper.ApplyForceToReachVelocity(this.rb, a * -this.speed, this.power, ForceMode.Force);
        }
        this.motor.SetPositionAndRotation(this.motor.position, base.transform.rotation * this.StartRotation * Quaternion.Euler(0f, 30f * (float)num, 0f));
        if (this.ParticleSystem != null)
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S))
            {
                this.ParticleSystem.Play();
            }
            else
            {
                this.ParticleSystem.Pause();
            }
        }
        bool flag = Vector3.Cross(this.orientation.forward, this.rb.velocity).y < 0f;
        this.rb.velocity = Quaternion.AngleAxis(Vector3.SignedAngle(this.rb.velocity, (flag ? 1f : 0f) * base.transform.forward, Vector3.up) * this.drag, Vector3.up) * this.rb.velocity;
    }

    // Token: 0x04000113 RID: 275
    public Transform motor;

    // Token: 0x04000114 RID: 276
    public float speed;

    // Token: 0x04000115 RID: 277
    public float drag;

    // Token: 0x04000116 RID: 278
    public float power;

    // Token: 0x04000117 RID: 279
    public float steerPower = 200f;

    // Token: 0x04000118 RID: 280
    public Transform orientation;

    // Token: 0x04000119 RID: 281
    private float horizontalInput;

    // Token: 0x0400011A RID: 282
    private float verticalInput;

    // Token: 0x0400011B RID: 283
    private Vector3 moveDirection;

    // Token: 0x0400011C RID: 284
    private Rigidbody rb;

    // Token: 0x0400011D RID: 285
    protected Quaternion StartRotation;

    // Token: 0x0400011E RID: 286
    protected ParticleSystem ParticleSystem;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000035 RID: 53
public class BoatSwitch : MonoBehaviour
{
    // Token: 0x06000126 RID: 294 RVA: 0x00006AC2 File Offset: 0x00004CC2
    private void Update()
    {
        if (this.pierIsNear && Input.GetKeyDown(KeyCode.LeftShift))
        {
            this.ChangeBoatState();
        }
    }

    // Token: 0x06000127 RID: 295 RVA: 0x00006AE0 File Offset: 0x00004CE0
    private void ChangeBoatState()
    {
        if (this.player.enabled)
        {
            this.player.gameObject.SetActive(false);
            this.boat.enabled = true;
            this.player.enabled = false;
            this.boatText.text = "Shift - подняться";
            this.player.transform.position = this.seatPos.position;
            this.player.transform.parent = base.transform;
            this.cam.currentMode = MoveCamera.UpdateMode.FixedUpdate;
            this.player.GetComponent<Rigidbody>().isKinematic = true;
            this.pierCollider.enabled = false;
            this.pierIsNear = false;
            this.boatText.gameObject.SetActive(false);
            if (this.trotil != null)
            {
                this.trotil.enabled = true;
                BeastBehaviour beastBehaviour = Object.FindObjectOfType<BeastBehaviour>();
                beastBehaviour.trotilSpawner.SetActive(false);
                beastBehaviour.OnBoatEntered();
            }
            if (this.sharkManager != null)
            {
                this.sharkManager.StartAttack();
            }
            this.player.gameObject.SetActive(true);
            return;
        }
        this.player.GetComponent<Rigidbody>().isKinematic = false;
        this.boat.enabled = false;
        this.player.enabled = true;
        this.boatText.text = "Shift - сесть в лодку";
        this.player.transform.position = this.pierPos.position;
        if (this.trotil != null)
        {
            this.trotil.enabled = false;
            BeastBehaviour beastBehaviour2 = Object.FindObjectOfType<BeastBehaviour>();
            if (beastBehaviour2 != null)
            {
                beastBehaviour2.OnEnterArea();
                beastBehaviour2.pier2.presence = "false";
                beastBehaviour2.pier2.SavePresence();
            }
        }
        this.player.transform.parent = null;
        this.cam.currentMode = MoveCamera.UpdateMode.LateUpdate;
    }

    // Token: 0x04000192 RID: 402
    public PlayerController player;

    // Token: 0x04000193 RID: 403
    public Collider pierCollider;

    // Token: 0x04000194 RID: 404
    public BoatController boat;

    // Token: 0x04000195 RID: 405
    public Transform seatPos;

    // Token: 0x04000196 RID: 406
    public Transform pierPos;

    // Token: 0x04000197 RID: 407
    public MoveCamera cam;

    // Token: 0x04000198 RID: 408
    public CurveSpawner trotil;

    // Token: 0x04000199 RID: 409
    public TextMeshProUGUI boatText;

    // Token: 0x0400019A RID: 410
    public SharkManager sharkManager;

    // Token: 0x0400019B RID: 411
    [HideInInspector]
    public bool pierIsNear;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Token: 0x02000008 RID: 8
public class BooBoss : MonoBehaviour
{
    // Token: 0x17000002 RID: 2
    // (get) Token: 0x06000011 RID: 17 RVA: 0x00002231 File Offset: 0x00000431
    // (set) Token: 0x06000012 RID: 18 RVA: 0x00002239 File Offset: 0x00000439
    public bool hasCompletedCutscene { get; set; }

    // Token: 0x17000003 RID: 3
    // (get) Token: 0x06000013 RID: 19 RVA: 0x00002242 File Offset: 0x00000442
    // (set) Token: 0x06000014 RID: 20 RVA: 0x0000224A File Offset: 0x0000044A
    public bool hasSeenTip { get; set; }

    // Token: 0x06000015 RID: 21 RVA: 0x00002253 File Offset: 0x00000453
    private void Awake()
    {
        this.agent = base.GetComponent<NavMeshAgent>();
        this.animator = base.GetComponentInChildren<Animator>();
        this.health = this.startHealth;
    }

    // Token: 0x06000016 RID: 22 RVA: 0x0000227C File Offset: 0x0000047C
    private void OnEnable()
    {
        CurveSpawner curveSpawner = this.phantomSpawner;
        curveSpawner.hasDoneSpawning = (Action)Delegate.Combine(curveSpawner.hasDoneSpawning, new Action(this.FinishPhantoms));
        CurveSpawner curveSpawner2 = this.phantomHardSpawner;
        curveSpawner2.hasDoneSpawning = (Action)Delegate.Combine(curveSpawner2.hasDoneSpawning, new Action(this.FinishPhantoms));
    }

    // Token: 0x06000017 RID: 23 RVA: 0x000022D8 File Offset: 0x000004D8
    private void OnDisable()
    {
        CurveSpawner curveSpawner = this.phantomSpawner;
        curveSpawner.hasDoneSpawning = (Action)Delegate.Remove(curveSpawner.hasDoneSpawning, new Action(this.FinishPhantoms));
        CurveSpawner curveSpawner2 = this.phantomHardSpawner;
        curveSpawner2.hasDoneSpawning = (Action)Delegate.Remove(curveSpawner2.hasDoneSpawning, new Action(this.FinishPhantoms));
    }

    // Token: 0x06000018 RID: 24 RVA: 0x00002333 File Offset: 0x00000533
    private void Start()
    {
        if (this.hasCompletedCutscene)
        {
            this.EnableBoss();
            return;
        }
        this.cutscene.SetActive(true);
    }

    // Token: 0x06000019 RID: 25 RVA: 0x00002350 File Offset: 0x00000550
    public void EnableBoss()
    {
        this.hasCompletedCutscene = true;
        this.cutsceneChecker.MeetBooBoss();
        this.healthSlider.gameObject.SetActive(true);
        this.damage = this.startDamage;
        this.agent.speed = this.startSpeed;
        this.isEnabled = true;
        MusicManager.instance.EnableMusic(5);
        base.GetComponent<SoundManager>().enabled = true;
    }

    // Token: 0x0600001A RID: 26 RVA: 0x000023BC File Offset: 0x000005BC
    private void Update()
    {
        if (this.isEnabled)
        {
            this.healthSlider.value = this.health / this.startHealth;
            this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
            this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
            if (!this.playerInSightRange && !this.playerInAttackRange)
            {
                this.Patroling();
            }
            if (this.playerInSightRange && !this.playerInAttackRange)
            {
                this.ChasePlayer();
            }
            if (this.playerInAttackRange && this.playerInSightRange)
            {
                this.AttackPlayer();
            }
        }
    }

    // Token: 0x0600001B RID: 27 RVA: 0x0000247C File Offset: 0x0000067C
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x0600001C RID: 28 RVA: 0x000024E0 File Offset: 0x000006E0
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x0600001D RID: 29 RVA: 0x00002586 File Offset: 0x00000786
    private void ChasePlayer()
    {
        this.agent.SetDestination(this.player.position);
        if (!this.isAttacking)
        {
            this.isAttacking = true;
            this.ChooseMoveset();
        }
    }

    // Token: 0x0600001E RID: 30 RVA: 0x000025B4 File Offset: 0x000007B4
    private void AttackPlayer()
    {
        this.agent.SetDestination(base.transform.position);
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x0600001F RID: 31 RVA: 0x00002650 File Offset: 0x00000850
    public void ChooseMoveset()
    {
        if (this.health < 170f && !this.hasActivatedForceField)
        {
            this.ActivateForceField();
            return;
        }
        if (this.health < 120f && !this.hasSummonedPhantoms && !this.forceField.activeInHierarchy)
        {
            this.SummonPhantoms();
            return;
        }
        int num = Random.Range(0, 3);
        if (num == 0)
        {
            base.StartCoroutine(this.SpawnGroupOfBullets(1, 6f));
            return;
        }
        if (num != 1)
        {
            if (num == 2)
            {
                if (this.health < 120f && !this.forceField.activeInHierarchy && this.canSummonPhantoms)
                {
                    this.SummonPhantoms();
                }
                else
                {
                    base.StartCoroutine(this.SpawnCandles());
                }
                if (this.health < 70f)
                {
                    for (int i = 0; i < 7; i++)
                    {
                        base.StartCoroutine(this.SummonLightning());
                    }
                }
            }
            return;
        }
        if (this.health > 170f || !this.canActivateForceField)
        {
            base.StartCoroutine(this.SpawnCandles());
            return;
        }
        this.ActivateForceField();
    }

    // Token: 0x06000020 RID: 32 RVA: 0x00002750 File Offset: 0x00000950
    public IEnumerator SpawnGroupOfBullets(int totalbullets, float cooldown)
    {
        int numberOfBullets = 0;
        while (numberOfBullets <= totalbullets)
        {
            this.SpawnBullet(80f);
            int num = numberOfBullets;
            numberOfBullets = num + 1;
            yield return new WaitForSeconds(cooldown);
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x06000021 RID: 33 RVA: 0x0000276D File Offset: 0x0000096D
    public IEnumerator SpawnCandles()
    {
        if (this.health > 100f)
        {
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(0.5f);
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(2f);
        }
        else
        {
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(0.5f);
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(0.5f);
            this.SpawnCandlesPack();
            yield return new WaitForSeconds(0.5f);
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x06000022 RID: 34 RVA: 0x0000277C File Offset: 0x0000097C
    public void SpawnCandlesPack()
    {
        this.SpawnCandle(new Vector3(-0.1f, 0f), -10f);
        this.SpawnCandle(new Vector3(0f, 0f), 0f);
        this.SpawnCandle(new Vector3(0.1f, 0f), 10f);
        if (this.health < 100f)
        {
            this.SpawnCandle(new Vector3(-0.2f, 0f), -20f);
            this.SpawnCandle(new Vector3(0.2f, 0f), 20f);
        }
    }

    // Token: 0x06000023 RID: 35 RVA: 0x00002818 File Offset: 0x00000A18
    public void SpawnCandle(Vector3 positionOffset, float rotationOffset)
    {
        this.attackPos.LookAt(this.player);
        Quaternion rotation = Quaternion.Euler(this.attackPos.eulerAngles.x + 90f, this.attackPos.eulerAngles.y + rotationOffset, this.attackPos.eulerAngles.z);
        Object.Instantiate<GameObject>(this.candle, this.attackPos.position + positionOffset, rotation);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
    }

    // Token: 0x06000024 RID: 36 RVA: 0x000028B0 File Offset: 0x00000AB0
    public void SpawnBullet(float rotationOffset)
    {
        this.attackPos.LookAt(this.player);
        this.newBullet = Object.Instantiate<GameObject>(this.bullet, this.attackPos.position, this.attackPos.rotation);
        this.newBullet.transform.parent = this.attackPos;
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
    }

    // Token: 0x06000025 RID: 37 RVA: 0x00002928 File Offset: 0x00000B28
    public void ActivateForceField()
    {
        if (!this.hasSeenTip)
        {
            this.hasSeenTip = true;
            this.tipTextAnimator.SetTrigger("isTipping");
            this.cutsceneChecker.HasSeenTip();
        }
        this.hasActivatedForceField = true;
        this.canActivateForceField = false;
        this.forceField.SetActive(true);
        this.forceField.GetComponent<Animator>().SetBool("isActivated", true);
        this.candleController.LightCandles();
        this.isAttacking = false;
    }

    // Token: 0x06000026 RID: 38 RVA: 0x000029A1 File Offset: 0x00000BA1
    public IEnumerator DeactivateForceField()
    {
        this.forceField.GetComponent<Animator>().SetBool("isActivated", false);
        yield return new WaitForSeconds(0.5f);
        this.forceField.SetActive(false);
        yield return new WaitForSeconds(15f);
        this.canActivateForceField = true;
        yield break;
    }

    // Token: 0x06000027 RID: 39 RVA: 0x000029B0 File Offset: 0x00000BB0
    public void SummonPhantoms()
    {
        this.hasSummonedPhantoms = true;
        this.canSummonPhantoms = false;
        this.damage = 0;
        this.agent.speed = 0f;
        Object.Instantiate<GameObject>(this.phantomEffect, base.transform.position, Quaternion.identity);
        foreach (object obj in base.transform)
        {
            ((Transform)obj).gameObject.SetActive(false);
        }
        if (this.health > 70f)
        {
            this.phantomSpawner.enabled = true;
            return;
        }
        this.phantomHardSpawner.enabled = true;
    }

    // Token: 0x06000028 RID: 40 RVA: 0x00002A74 File Offset: 0x00000C74
    public void FinishPhantoms()
    {
        base.StartCoroutine(this.ReturnBoss());
    }

    // Token: 0x06000029 RID: 41 RVA: 0x00002A83 File Offset: 0x00000C83
    public IEnumerator ReturnBoss()
    {
        this.damage = this.startDamage;
        this.agent.speed = this.startSpeed;
        this.canActivateForceField = false;
        foreach (object obj in base.transform)
        {
            ((Transform)obj).gameObject.SetActive(true);
        }
        this.isAttacking = false;
        yield return new WaitForSeconds(20f);
        this.canSummonPhantoms = true;
        this.canActivateForceField = true;
        yield break;
    }

    // Token: 0x0600002A RID: 42 RVA: 0x00002A92 File Offset: 0x00000C92
    public IEnumerator SummonLightning()
    {
        Vector3 lightningPos = this.RandomPointInBox(new Vector3(18f, 0f, 18f));
        Object.Instantiate<GameObject>(this.preLightning, lightningPos, Quaternion.identity);
        yield return new WaitForSeconds(1f);
        Object.Instantiate<GameObject>(this.lightning, lightningPos, Quaternion.identity);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
        yield break;
    }

    // Token: 0x0600002B RID: 43 RVA: 0x00002AA4 File Offset: 0x00000CA4
    private Vector3 RandomPointInBox(Vector3 size)
    {
        return new Vector3(this.player.position.x, 0f, this.player.position.z) + new Vector3((Random.value - 0.5f) * size.x, (Random.value - 0.5f) * size.y, (Random.value - 0.5f) * size.z);
    }

    // Token: 0x0600002C RID: 44 RVA: 0x00002B1B File Offset: 0x00000D1B
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x0600002D RID: 45 RVA: 0x00002B24 File Offset: 0x00000D24
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x0600002E RID: 46 RVA: 0x00002B54 File Offset: 0x00000D54
    private void DestroyEnemy()
    {
        this.candleController.BlowOutCandles();
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.finalMushroom, base.transform.position, Quaternion.identity);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x0600002F RID: 47 RVA: 0x00002BBC File Offset: 0x00000DBC
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x0400000F RID: 15
    public LayerMask whatIsGround;

    // Token: 0x04000010 RID: 16
    public LayerMask whatIsPlayer;

    // Token: 0x04000011 RID: 17
    public Vector3 walkPoint;

    // Token: 0x04000012 RID: 18
    private bool walkPointSet;

    // Token: 0x04000013 RID: 19
    public float walkPointRange;

    // Token: 0x04000014 RID: 20
    public float timeBetweenAttacks;

    // Token: 0x04000015 RID: 21
    private bool isAttacking;

    // Token: 0x04000016 RID: 22
    private bool alreadyAttacked;

    // Token: 0x04000017 RID: 23
    public float sightRange;

    // Token: 0x04000018 RID: 24
    public float attackRange;

    // Token: 0x04000019 RID: 25
    public bool playerInSightRange;

    // Token: 0x0400001A RID: 26
    public bool playerInAttackRange;

    // Token: 0x0400001D RID: 29
    public float startHealth;

    // Token: 0x0400001E RID: 30
    public float health;

    // Token: 0x0400001F RID: 31
    public int startDamage;

    // Token: 0x04000020 RID: 32
    public float startSpeed;

    // Token: 0x04000021 RID: 33
    private int damage;

    // Token: 0x04000022 RID: 34
    private int restCount;

    // Token: 0x04000023 RID: 35
    private bool canActivateForceField = true;

    // Token: 0x04000024 RID: 36
    private bool canSummonPhantoms = true;

    // Token: 0x04000025 RID: 37
    private bool hasActivatedForceField;

    // Token: 0x04000026 RID: 38
    private bool hasSummonedPhantoms;

    // Token: 0x04000027 RID: 39
    private bool isEnabled;

    // Token: 0x04000028 RID: 40
    public GameObject cutscene;

    // Token: 0x04000029 RID: 41
    public GameObject bullet;

    // Token: 0x0400002A RID: 42
    public GameObject candle;

    // Token: 0x0400002B RID: 43
    public GameObject forceField;

    // Token: 0x0400002C RID: 44
    public GameObject preLightning;

    // Token: 0x0400002D RID: 45
    public GameObject lightning;

    // Token: 0x0400002E RID: 46
    public Transform attackPos;

    // Token: 0x0400002F RID: 47
    public Transform lightningCenter;

    // Token: 0x04000030 RID: 48
    public GameObject magicSound;

    // Token: 0x04000031 RID: 49
    public GameObject phantomEffect;

    // Token: 0x04000032 RID: 50
    public GameObject deathEffect;

    // Token: 0x04000033 RID: 51
    public GameObject finalMushroom;

    // Token: 0x04000034 RID: 52
    public CandleController candleController;

    // Token: 0x04000035 RID: 53
    public CurveSpawner phantomSpawner;

    // Token: 0x04000036 RID: 54
    public CurveSpawner phantomHardSpawner;

    // Token: 0x04000037 RID: 55
    public CutsceneChecker cutsceneChecker;

    // Token: 0x04000038 RID: 56
    public Animator tipTextAnimator;

    // Token: 0x04000039 RID: 57
    public Slider healthSlider;

    // Token: 0x0400003A RID: 58
    private NavMeshAgent agent;

    // Token: 0x0400003B RID: 59
    public Transform player;

    // Token: 0x0400003C RID: 60
    public Transform playerLookPoint;

    // Token: 0x0400003D RID: 61
    private Animator animator;

    // Token: 0x0400003E RID: 62
    private GameObject newBullet;

    // Token: 0x0400003F RID: 63
    private Vector3 stickDirection;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x0200004D RID: 77
public class BooTrigger : MonoBehaviour
{
    // Token: 0x0600018B RID: 395 RVA: 0x000080B0 File Offset: 0x000062B0
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.border.SetActive(false);
            this.doorsAnimator.SetTrigger("isOpened");
            this.navMeshAgent.speed = 8f;
            Object.Instantiate<GameObject>(this.sound, other.transform.position, Quaternion.identity).transform.parent = other.transform;
            MusicManager.instance.EnableMusic(1);
            base.GetComponent<Collider>().enabled = false;
        }
    }

    // Token: 0x0400020E RID: 526
    [SerializeField]
    private Animator doorsAnimator;

    // Token: 0x0400020F RID: 527
    [SerializeField]
    private NavMeshAgent navMeshAgent;

    // Token: 0x04000210 RID: 528
    [SerializeField]
    private GameObject sound;

    // Token: 0x04000211 RID: 529
    [SerializeField]
    private GameObject border;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Token: 0x02000009 RID: 9
public class Boss : MonoBehaviour
{
    // Token: 0x06000031 RID: 49 RVA: 0x00002C20 File Offset: 0x00000E20
    private void Awake()
    {
        this.player = GameObject.FindGameObjectWithTag("Player").transform;
        this.agent = base.GetComponent<NavMeshAgent>();
        this.health = this.startHealth;
        this.healthSlider = Object.FindObjectOfType<MushroomGame>().bossHP.GetComponent<Slider>();
    }

    // Token: 0x06000032 RID: 50 RVA: 0x00002C6F File Offset: 0x00000E6F
    private void Start()
    {
        this.healthSlider.gameObject.SetActive(true);
    }

    // Token: 0x06000033 RID: 51 RVA: 0x00002C84 File Offset: 0x00000E84
    private void Update()
    {
        this.healthSlider.value = this.health / this.startHealth;
        this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
        if (!this.playerInSightRange && !this.playerInAttackRange)
        {
            this.Patroling();
        }
        if (this.playerInSightRange && !this.playerInAttackRange)
        {
            this.ChasePlayer();
        }
        if (this.playerInAttackRange && this.playerInSightRange)
        {
            this.AttackPlayer();
        }
    }

    // Token: 0x06000034 RID: 52 RVA: 0x00002D3C File Offset: 0x00000F3C
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x06000035 RID: 53 RVA: 0x00002DA0 File Offset: 0x00000FA0
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x06000036 RID: 54 RVA: 0x00002E46 File Offset: 0x00001046
    private void ChasePlayer()
    {
        this.agent.SetDestination(this.player.position);
        if (!this.isAttacking)
        {
            this.isAttacking = true;
            this.ChooseMoveset();
        }
    }

    // Token: 0x06000037 RID: 55 RVA: 0x00002E74 File Offset: 0x00001074
    private void AttackPlayer()
    {
        this.agent.SetDestination(base.transform.position);
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x06000038 RID: 56 RVA: 0x00002F10 File Offset: 0x00001110
    public void ChooseMoveset()
    {
        int num = Random.Range(0, 3);
        if (num == 0)
        {
            base.StartCoroutine(this.SpawnGroupSticks(false, 5, 0.3f));
            return;
        }
        if (num != 1)
        {
            if (num == 2)
            {
                for (int i = 0; i < 7; i++)
                {
                    base.StartCoroutine(this.SummonLightning());
                }
            }
            return;
        }
        if (this.health > 15f)
        {
            base.StartCoroutine(this.SpawnGroupSticks(true, 3, 0.6f));
            return;
        }
        base.StartCoroutine(this.InstantiateEarthquake());
    }

    // Token: 0x06000039 RID: 57 RVA: 0x00002F8E File Offset: 0x0000118E
    public IEnumerator SpawnGroupSticks(bool isNormal, int totalSticks, float cooldown)
    {
        int numberOfSticks = 0;
        while (numberOfSticks <= totalSticks)
        {
            this.SpawnStick(isNormal);
            int num = numberOfSticks;
            numberOfSticks = num + 1;
            yield return new WaitForSeconds(cooldown);
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x0600003A RID: 58 RVA: 0x00002FB4 File Offset: 0x000011B4
    public void SpawnStick(bool isNormal)
    {
        if (isNormal)
        {
            Object.Instantiate<GameObject>(this.stick, this.attackPosNormal.position, Quaternion.Euler(0f, (float)Random.Range(0, 360), (float)Random.Range(0, 360)));
        }
        else
        {
            Object.Instantiate<GameObject>(this.stick, this.attackPosDown.position, this.attackPosDown.rotation);
        }
        Object.Instantiate<GameObject>(this.magicSound, this.attackPosNormal.position, Quaternion.identity);
    }

    // Token: 0x0600003B RID: 59 RVA: 0x0000303D File Offset: 0x0000123D
    public IEnumerator SummonLightning()
    {
        Vector3 lightningPos = this.RandomPointInBox(new Vector3(15f, 0f, 15f));
        Object.Instantiate<GameObject>(this.preLightning, lightningPos, Quaternion.identity);
        yield return new WaitForSeconds(1f);
        Object.Instantiate<GameObject>(this.lightning, lightningPos, Quaternion.identity);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPosNormal.position, Quaternion.identity);
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x0600003C RID: 60 RVA: 0x0000304C File Offset: 0x0000124C
    public IEnumerator InstantiateEarthquake()
    {
        Object.Instantiate<GameObject>(this.earthquake, this.lightningCenter.position, this.lightningCenter.rotation);
        yield return new WaitForSeconds(3f);
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x0600003D RID: 61 RVA: 0x0000305C File Offset: 0x0000125C
    private Vector3 RandomPointInBox(Vector3 size)
    {
        return new Vector3(this.player.position.x, 0f, this.player.position.z) + new Vector3((Random.value - 0.5f) * size.x, (Random.value - 0.5f) * size.y, (Random.value - 0.5f) * size.z);
    }

    // Token: 0x0600003E RID: 62 RVA: 0x000030D3 File Offset: 0x000012D3
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x0600003F RID: 63 RVA: 0x000030DC File Offset: 0x000012DC
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x06000040 RID: 64 RVA: 0x0000310C File Offset: 0x0000130C
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.finalMushroom, base.transform.position, Quaternion.identity);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x06000041 RID: 65 RVA: 0x00003168 File Offset: 0x00001368
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x04000040 RID: 64
    public LayerMask whatIsGround;

    // Token: 0x04000041 RID: 65
    public LayerMask whatIsPlayer;

    // Token: 0x04000042 RID: 66
    public Vector3 walkPoint;

    // Token: 0x04000043 RID: 67
    private bool walkPointSet;

    // Token: 0x04000044 RID: 68
    public float walkPointRange;

    // Token: 0x04000045 RID: 69
    public float timeBetweenAttacks;

    // Token: 0x04000046 RID: 70
    private bool isAttacking;

    // Token: 0x04000047 RID: 71
    private bool alreadyAttacked;

    // Token: 0x04000048 RID: 72
    public float sightRange;

    // Token: 0x04000049 RID: 73
    public float attackRange;

    // Token: 0x0400004A RID: 74
    public bool playerInSightRange;

    // Token: 0x0400004B RID: 75
    public bool playerInAttackRange;

    // Token: 0x0400004C RID: 76
    public float startHealth;

    // Token: 0x0400004D RID: 77
    private float health;

    // Token: 0x0400004E RID: 78
    public int damage;

    // Token: 0x0400004F RID: 79
    public GameObject stick;

    // Token: 0x04000050 RID: 80
    public GameObject preLightning;

    // Token: 0x04000051 RID: 81
    public GameObject lightning;

    // Token: 0x04000052 RID: 82
    public GameObject earthquake;

    // Token: 0x04000053 RID: 83
    public Transform attackPosNormal;

    // Token: 0x04000054 RID: 84
    public Transform attackPosDown;

    // Token: 0x04000055 RID: 85
    public Transform lightningCenter;

    // Token: 0x04000056 RID: 86
    public GameObject magicSound;

    // Token: 0x04000057 RID: 87
    public GameObject deathEffect;

    // Token: 0x04000058 RID: 88
    public GameObject finalMushroom;

    // Token: 0x04000059 RID: 89
    public Slider healthSlider;

    // Token: 0x0400005A RID: 90
    private NavMeshAgent agent;

    // Token: 0x0400005B RID: 91
    private Transform player;

    // Token: 0x0400005C RID: 92
    private GameObject newStick;

    // Token: 0x0400005D RID: 93
    private Vector3 stickDirection;
}

using System;
using UnityEngine;

// Token: 0x0200000A RID: 10
public class BossMovesets : MonoBehaviour
{
    // Token: 0x06000043 RID: 67 RVA: 0x000031BD File Offset: 0x000013BD
    private void Start()
    {
    }

    // Token: 0x06000044 RID: 68 RVA: 0x000031BF File Offset: 0x000013BF
    private void Update()
    {
    }
}

using System;
using UnityEngine;

// Token: 0x02000003 RID: 3
public class BossMusicSwitcher : MonoBehaviour
{
    // Token: 0x06000004 RID: 4 RVA: 0x0000205C File Offset: 0x0000025C
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (MusicManager.instance.audioSource.clip != MusicManager.instance.musicVariants[5])
            {
                MusicManager.instance.EnableMusic(5);
                this.boss.SetActive(true);
            }
            else
            {
                MusicManager.instance.EnableMusic(0);
            }
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x04000001 RID: 1
    public GameObject boss;
}

using System;
using UnityEngine;

// Token: 0x0200004E RID: 78
public class BossTeleport : MonoBehaviour
{
    // Token: 0x0600018D RID: 397 RVA: 0x00008140 File Offset: 0x00006340
    private void OnEnable()
    {
        ItemCollector itemCollector = this.itemCollector;
        itemCollector.hasCollected = (Action)Delegate.Combine(itemCollector.hasCollected, new Action(this.CheckFlags));
    }

    // Token: 0x0600018E RID: 398 RVA: 0x00008169 File Offset: 0x00006369
    private void OnDisable()
    {
        ItemCollector itemCollector = this.itemCollector;
        itemCollector.hasCollected = (Action)Delegate.Remove(itemCollector.hasCollected, new Action(this.CheckFlags));
    }

    // Token: 0x0600018F RID: 399 RVA: 0x00008192 File Offset: 0x00006392
    private void Start()
    {
        this.CheckFlags();
    }

    // Token: 0x06000190 RID: 400 RVA: 0x0000819A File Offset: 0x0000639A
    private void CheckFlags()
    {
        if (this.itemCollector.items >= this.flagsCount)
        {
            this.EnablePortal();
        }
    }

    // Token: 0x06000191 RID: 401 RVA: 0x000081B5 File Offset: 0x000063B5
    private void EnablePortal()
    {
        base.GetComponent<BoxCollider>().enabled = true;
        base.transform.GetChild(0).gameObject.SetActive(true);
    }

    // Token: 0x06000192 RID: 402 RVA: 0x000081DC File Offset: 0x000063DC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.root.position = this.teleportPoint.position;
            GravityController.instance.SetEarthGravity();
            base.Invoke("ActivateBoss", 0.5f);
        }
    }

    // Token: 0x06000193 RID: 403 RVA: 0x0000822B File Offset: 0x0000642B
    public void ActivateBoss()
    {
        Debug.Log("ACTIVATIN");
        this.boss.SetActive(true);
    }

    // Token: 0x04000212 RID: 530
    [SerializeField]
    private GameObject boss;

    // Token: 0x04000213 RID: 531
    [SerializeField]
    private Transform teleportPoint;

    // Token: 0x04000214 RID: 532
    [SerializeField]
    private ItemCollector itemCollector;

    // Token: 0x04000215 RID: 533
    [SerializeField]
    private int flagsCount;
}

using System;
using UnityEngine;

// Token: 0x02000002 RID: 2
public class box : MonoBehaviour
{
    // Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
    private void Start()
    {
    }

    // Token: 0x06000002 RID: 2 RVA: 0x00002052 File Offset: 0x00000252
    private void Update()
    {
    }
}

using System;
using UnityEngine;

// Token: 0x02000036 RID: 54
public class BuoyancyObject : MonoBehaviour
{
    // Token: 0x06000129 RID: 297 RVA: 0x00006CC8 File Offset: 0x00004EC8
    private void FixedUpdate()
    {
        this.FloatersUnderWater = 0;
        for (int i = 0; i < this.Floaters.Length; i++)
        {
            float num = this.Floaters[i].position.y - this.WaterHeight;
            if (num < 0f)
            {
                this.Rb.AddForceAtPosition(Vector3.up * this.FloatingPower * Mathf.Abs(num), this.Floaters[i].position, ForceMode.Force);
                this.FloatersUnderWater++;
                if (!this.Underwater)
                {
                    this.Underwater = true;
                    this.SwitchState(true);
                }
            }
        }
        if (this.Underwater && this.FloatersUnderWater == 0)
        {
            this.Underwater = false;
            this.SwitchState(false);
        }
    }

    // Token: 0x0600012A RID: 298 RVA: 0x00006D8C File Offset: 0x00004F8C
    private void SwitchState(bool isUnderwater)
    {
        if (isUnderwater)
        {
            this.Rb.drag = this.UnderWaterDrag;
            this.Rb.angularDrag = this.UnderWaterAngularDrag;
            return;
        }
        this.Rb.drag = this.AirDrag;
        this.Rb.angularDrag = this.AirAngularDrag;
    }

    // Token: 0x0400019C RID: 412
    public Transform[] Floaters;

    // Token: 0x0400019D RID: 413
    public float UnderWaterDrag = 3f;

    // Token: 0x0400019E RID: 414
    public float UnderWaterAngularDrag = 1f;

    // Token: 0x0400019F RID: 415
    public float AirDrag;

    // Token: 0x040001A0 RID: 416
    public float AirAngularDrag = 0.05f;

    // Token: 0x040001A1 RID: 417
    public float FloatingPower = 15f;

    // Token: 0x040001A2 RID: 418
    public float WaterHeight;

    // Token: 0x040001A3 RID: 419
    public Rigidbody Rb;

    // Token: 0x040001A4 RID: 420
    private bool Underwater;

    // Token: 0x040001A5 RID: 421
    private int FloatersUnderWater;
}

using System;
using System.Collections;
using UnityEngine;

// Token: 0x02000021 RID: 33
public class BurgerBehaviour : MonoBehaviour
{
    // Token: 0x060000C6 RID: 198 RVA: 0x0000534F File Offset: 0x0000354F
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.burgerUsual.SetActive(false);
            this.burgerUnusual.SetActive(true);
        }
    }

    // Token: 0x060000C7 RID: 199 RVA: 0x00005376 File Offset: 0x00003576
    public IEnumerator OnBurgerDestroyed()
    {
        this.spawner.enabled = true;
        yield return new WaitForSeconds(15.5f);
        this.spawner.enabled = false;
        this.lockUI.OnFoodKilled();
        yield break;
    }

    // Token: 0x0400011F RID: 287
    public GameObject burgerUsual;

    // Token: 0x04000120 RID: 288
    public GameObject burgerUnusual;

    // Token: 0x04000121 RID: 289
    public CurveSpawner spawner;

    // Token: 0x04000122 RID: 290
    public LockUI lockUI;
}

using System;
using UnityEngine;

// Token: 0x0200004F RID: 79
public class Candle : MonoBehaviour
{
    // Token: 0x06000195 RID: 405 RVA: 0x0000824C File Offset: 0x0000644C
    public void BlowOutCandle()
    {
        if (this.isLit)
        {
            this.isLit = false;
            Object.Instantiate<GameObject>(this.bang, this.bangPoint.position, Quaternion.identity);
            Action action = this.isBlown;
            if (action != null)
            {
                action();
            }
            this.LightCandle(false);
        }
    }

    // Token: 0x06000196 RID: 406 RVA: 0x0000829C File Offset: 0x0000649C
    public void LightCandle(bool state)
    {
        base.transform.GetChild(0).gameObject.SetActive(state);
        this.isLit = state;
    }

    // Token: 0x04000216 RID: 534
    public bool isLit;

    // Token: 0x04000217 RID: 535
    public Action isBlown;

    // Token: 0x04000218 RID: 536
    [SerializeField]
    private Transform bangPoint;

    // Token: 0x04000219 RID: 537
    [SerializeField]
    private GameObject bang;
}

using System;
using UnityEngine;

// Token: 0x0200002C RID: 44
public class CandleBullet : MonoBehaviour
{
    // Token: 0x060000F5 RID: 245 RVA: 0x00005D04 File Offset: 0x00003F04
    private void Update()
    {
        base.transform.Translate(Vector3.up * this.speed * Time.deltaTime);
    }

    // Token: 0x04000151 RID: 337
    [SerializeField]
    private float speed;
}

using System;
using UnityEngine;

// Token: 0x02000050 RID: 80
public class CandleController : MonoBehaviour
{
    // Token: 0x06000198 RID: 408 RVA: 0x000082C4 File Offset: 0x000064C4
    private void OnEnable()
    {
        foreach (Candle candle in this.candles)
        {
            candle.isBlown = (Action)Delegate.Combine(candle.isBlown, new Action(this.CheckCandles));
        }
    }

    // Token: 0x06000199 RID: 409 RVA: 0x0000830C File Offset: 0x0000650C
    private void OnDisable()
    {
        foreach (Candle candle in this.candles)
        {
            candle.isBlown = (Action)Delegate.Remove(candle.isBlown, new Action(this.CheckCandles));
        }
    }

    // Token: 0x0600019A RID: 410 RVA: 0x00008354 File Offset: 0x00006554
    public void LightCandles()
    {
        Candle[] array = this.candles;
        for (int i = 0; i < array.Length; i++)
        {
            array[i].LightCandle(true);
        }
    }

    // Token: 0x0600019B RID: 411 RVA: 0x00008380 File Offset: 0x00006580
    public void BlowOutCandles()
    {
        Candle[] array = this.candles;
        for (int i = 0; i < array.Length; i++)
        {
            array[i].BlowOutCandle();
        }
    }

    // Token: 0x0600019C RID: 412 RVA: 0x000083AC File Offset: 0x000065AC
    public void CheckCandles()
    {
        if (this.boss != null)
        {
            this.boss.TakeDamage(2);
        }
        Candle[] array = this.candles;
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i].isLit)
            {
                return;
            }
        }
        if (this.boss != null)
        {
            base.StartCoroutine(this.boss.DeactivateForceField());
        }
    }

    // Token: 0x0400021A RID: 538
    [SerializeField]
    private Candle[] candles;

    // Token: 0x0400021B RID: 539
    [SerializeField]
    private BooBoss boss;
}

using System;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x02000033 RID: 51
public class Card : MonoBehaviour
{
    // Token: 0x0600010F RID: 271 RVA: 0x00006492 File Offset: 0x00004692
    private void Start()
    {
        this.animator = base.GetComponent<Animator>();
        this.cardImage = base.GetComponent<Image>();
        this.cardManager = Object.FindObjectOfType<CardManager>();
        this.cardImage.sprite = this.backSprite;
    }

    // Token: 0x06000110 RID: 272 RVA: 0x000064C8 File Offset: 0x000046C8
    public void OnClick()
    {
        if (!this.cardManager.CanFlipCard(this))
        {
            return;
        }
        this.animator.SetBool("isFaceUp", true);
        this.animator.SetTrigger("isTriggered");
        this.cardManager.CardRevealed(this);
    }

    // Token: 0x06000111 RID: 273 RVA: 0x00006506 File Offset: 0x00004706
    public void RevealFace()
    {
        this.cardImage.sprite = this.faceSprite;
    }

    // Token: 0x06000112 RID: 274 RVA: 0x00006519 File Offset: 0x00004719
    public void Hide()
    {
        this.animator.SetBool("isFaceUp", false);
        this.animator.SetTrigger("isTriggered");
    }

    // Token: 0x06000113 RID: 275 RVA: 0x0000653C File Offset: 0x0000473C
    public void RevealBack()
    {
        this.cardImage.sprite = this.backSprite;
    }

    // Token: 0x06000114 RID: 276 RVA: 0x0000654F File Offset: 0x0000474F
    public void BooAnimation()
    {
        this.animator.SetTrigger("screamer");
    }

    // Token: 0x04000177 RID: 375
    private Animator animator;

    // Token: 0x04000178 RID: 376
    public Sprite faceSprite;

    // Token: 0x04000179 RID: 377
    public Sprite backSprite;

    // Token: 0x0400017A RID: 378
    private Image cardImage;

    // Token: 0x0400017B RID: 379
    private CardManager cardManager;
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x02000034 RID: 52
public class CardManager : MonoBehaviour
{
    // Token: 0x06000116 RID: 278 RVA: 0x00006569 File Offset: 0x00004769
    private void Start()
    {
    }

    // Token: 0x06000117 RID: 279 RVA: 0x0000656B File Offset: 0x0000476B
    private void OnEnable()
    {
        this.DeactivatePlayer();
    }

    // Token: 0x06000118 RID: 280 RVA: 0x00006573 File Offset: 0x00004773
    private void OnDisable()
    {
        this.ActivatePlayer();
    }

    // Token: 0x06000119 RID: 281 RVA: 0x0000657C File Offset: 0x0000477C
    public void StartGame(List<Sprite> cardFaces)
    {
        Vector2 vector = new Vector2((float)(-(float)this.gridCols) / 2f, (float)this.gridRows / 2f);
        List<Sprite> list = new List<Sprite>(cardFaces);
        list.AddRange(cardFaces);
        this.Shuffle(list);
        for (int i = 0; i < this.gridRows; i++)
        {
            for (int j = 0; j < this.gridCols; j++)
            {
                GameObject gameObject = Object.Instantiate<GameObject>(this.cardPrefab, this.cardParent);
                Card component = gameObject.GetComponent<Card>();
                int index = i * this.gridCols + j;
                component.faceSprite = list[index];
                RectTransform component2 = gameObject.GetComponent<RectTransform>();
                float x = vector.x + (float)j * (this.cellWidth + this.offsetX);
                float y = vector.y - (float)i * (this.cellHeight + this.offsetY);
                component2.anchoredPosition = new Vector2(x, y);
            }
        }
    }

    // Token: 0x0600011A RID: 282 RVA: 0x00006660 File Offset: 0x00004860
    public void CardRevealed(Card card)
    {
        if (this.firstRevealed == null)
        {
            this.firstRevealed = card;
            if (card.faceSprite == this.booSprite && !this._hasSeenBoo)
            {
                this._hasSeenBoo = true;
                Object.Instantiate<GameObject>(this.screamerSound, this.player.transform.position, Quaternion.identity);
                Object.Instantiate<GameObject>(this.booSound, this.player.transform.position, Quaternion.identity);
                card.BooAnimation();
                return;
            }
        }
        else
        {
            this.secondRevealed = card;
            if (card.faceSprite == this.booSprite && !this._hasSeenBoo)
            {
                this._hasSeenBoo = true;
                Object.Instantiate<GameObject>(this.screamerSound, this.player.transform.position, Quaternion.identity);
                Object.Instantiate<GameObject>(this.booSound, this.player.transform.position, Quaternion.identity);
                card.BooAnimation();
            }
            base.StartCoroutine(this.CheckMatch());
        }
    }

    // Token: 0x0600011B RID: 283 RVA: 0x00006771 File Offset: 0x00004971
    private IEnumerator CheckMatch()
    {
        if (this.firstRevealed.faceSprite == this.secondRevealed.faceSprite)
        {
            yield return new WaitForSeconds(0.5f);
            this.MakeCardInvisible(this.firstRevealed);
            this.MakeCardInvisible(this.secondRevealed);
        }
        else
        {
            yield return new WaitForSeconds(1f);
            this.firstRevealed.Hide();
            this.secondRevealed.Hide();
        }
        this.firstRevealed = null;
        this.secondRevealed = null;
        this.CheckLevelComplete();
        yield break;
    }

    // Token: 0x0600011C RID: 284 RVA: 0x00006780 File Offset: 0x00004980
    private void MakeCardInvisible(Card card)
    {
        Image component = card.GetComponent<Image>();
        if (component != null)
        {
            component.color = new Color(1f, 1f, 1f, 0f);
        }
        card.GetComponent<Button>().interactable = false;
    }

    // Token: 0x0600011D RID: 285 RVA: 0x000067C8 File Offset: 0x000049C8
    private void CheckLevelComplete()
    {
        using (IEnumerator enumerator = this.cardParent.GetEnumerator())
        {
            while (enumerator.MoveNext())
            {
                if (((Transform)enumerator.Current).GetComponent<Image>().color.a > 0f)
                {
                    return;
                }
            }
        }
        if (this.levelToLoad < 3)
        {
            this.levelToLoad++;
            this.LoadLevel(this.levelToLoad);
        }
        else
        {
            this.memoryGameTrigger.OnGameOver();
        }
        Debug.Log("Level Complete!");
    }

    // Token: 0x0600011E RID: 286 RVA: 0x0000686C File Offset: 0x00004A6C
    public bool CanFlipCard(Card card)
    {
        return card != this.firstRevealed && this.secondRevealed == null;
    }

    // Token: 0x0600011F RID: 287 RVA: 0x0000688C File Offset: 0x00004A8C
    private void Shuffle(List<Sprite> deck)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            Sprite value = deck[i];
            int index = Random.Range(i, deck.Count);
            deck[i] = deck[index];
            deck[index] = value;
        }
    }

    // Token: 0x06000120 RID: 288 RVA: 0x000068D8 File Offset: 0x00004AD8
    private void ClearPreviousCards()
    {
        foreach (object obj in this.cardParent)
        {
            Object.Destroy(((Transform)obj).gameObject);
        }
    }

    // Token: 0x06000121 RID: 289 RVA: 0x00006934 File Offset: 0x00004B34
    public void LoadLevel(int level)
    {
        this.ClearPreviousCards();
        switch (level)
        {
            case 1:
                this.gridRows = 2;
                this.gridCols = 4;
                this.gridLayoutManager.ConfigureGrid(this.gridRows, this.gridCols, 150f, 150f, 10f, 10f);
                this.StartGame(this.cardFacesLevel1);
                return;
            case 2:
                this.gridRows = 3;
                this.gridCols = 4;
                this.gridLayoutManager.ConfigureGrid(this.gridRows, this.gridCols, 150f, 150f, 10f, 10f);
                this.StartGame(this.cardFacesLevel2);
                return;
            case 3:
                this.gridRows = 4;
                this.gridCols = 4;
                this.gridLayoutManager.ConfigureGrid(this.gridRows, this.gridCols, 150f, 150f, 10f, 10f);
                this.StartGame(this.cardFacesLevel3);
                return;
            default:
                return;
        }
    }

    // Token: 0x06000122 RID: 290 RVA: 0x00006A2D File Offset: 0x00004C2D
    public void Restart()
    {
        this.LoadLevel(this.levelToLoad);
    }

    // Token: 0x06000123 RID: 291 RVA: 0x00006A3B File Offset: 0x00004C3B
    public void ActivatePlayer()
    {
        this.gun.SetActive(true);
        this.player.SetupDialogue(false);
        base.gameObject.SetActive(false);
    }

    // Token: 0x06000124 RID: 292 RVA: 0x00006A61 File Offset: 0x00004C61
    public void DeactivatePlayer()
    {
        this.gun.SetActive(false);
        this.player.SetupDialogue(true);
        this.LoadLevel(this.levelToLoad);
    }

    // Token: 0x0400017C RID: 380
    public List<Sprite> cardFacesLevel1;

    // Token: 0x0400017D RID: 381
    public List<Sprite> cardFacesLevel2;

    // Token: 0x0400017E RID: 382
    public List<Sprite> cardFacesLevel3;

    // Token: 0x0400017F RID: 383
    public GameObject cardPrefab;

    // Token: 0x04000180 RID: 384
    public Transform cardParent;

    // Token: 0x04000181 RID: 385
    public GameObject gun;

    // Token: 0x04000182 RID: 386
    public Sprite booSprite;

    // Token: 0x04000183 RID: 387
    public PlayerController player;

    // Token: 0x04000184 RID: 388
    public GridLayoutManager gridLayoutManager;

    // Token: 0x04000185 RID: 389
    public MemoryGameTrigger memoryGameTrigger;

    // Token: 0x04000186 RID: 390
    public GameObject screamerSound;

    // Token: 0x04000187 RID: 391
    public GameObject booSound;

    // Token: 0x04000188 RID: 392
    private Card firstRevealed;

    // Token: 0x04000189 RID: 393
    private Card secondRevealed;

    // Token: 0x0400018A RID: 394
    public float cellWidth = 150f;

    // Token: 0x0400018B RID: 395
    public float cellHeight = 150f;

    // Token: 0x0400018C RID: 396
    public float offsetX = 10f;

    // Token: 0x0400018D RID: 397
    public float offsetY = 10f;

    // Token: 0x0400018E RID: 398
    public int gridRows;

    // Token: 0x0400018F RID: 399
    public int gridCols;

    // Token: 0x04000190 RID: 400
    private int levelToLoad = 1;

    // Token: 0x04000191 RID: 401
    private bool _hasSeenBoo;
}

using System;
using UnityEngine;

// Token: 0x02000040 RID: 64
public class CaveSoundDetector : MonoBehaviour
{
    // Token: 0x06000155 RID: 341 RVA: 0x000078CE File Offset: 0x00005ACE
    private void Start()
    {
        this.music = Object.FindObjectOfType<MusicManager>();
    }

    // Token: 0x06000156 RID: 342 RVA: 0x000078DB File Offset: 0x00005ADB
    private void OnTriggerEnter(Collider other)
    {
        other.CompareTag("Player");
    }

    // Token: 0x040001E3 RID: 483
    private MusicManager music;
}

using System;
using UnityEngine;

// Token: 0x02000051 RID: 81
public class ChaseOver : MonoBehaviour
{
    // Token: 0x0600019E RID: 414 RVA: 0x0000841C File Offset: 0x0000661C
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.enemy.TakeDamage(10000);
            this.border.SetActive(true);
            this.player.moveSpeed = 3.5f;
            MusicManager.instance.EnableMusic(0);
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x0400021C RID: 540
    [SerializeField]
    private Enemy enemy;

    // Token: 0x0400021D RID: 541
    [SerializeField]
    private PlayerController player;

    // Token: 0x0400021E RID: 542
    [SerializeField]
    private GameObject border;
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Token: 0x0200006A RID: 106
public class ChatController : MonoBehaviour
{
    // Token: 0x06000202 RID: 514 RVA: 0x0000A16B File Offset: 0x0000836B
    private void OnEnable()
    {
        this.TMP_ChatInput.onSubmit.AddListener(new UnityAction<string>(this.AddToChatOutput));
    }

    // Token: 0x06000203 RID: 515 RVA: 0x0000A189 File Offset: 0x00008389
    private void OnDisable()
    {
        this.TMP_ChatInput.onSubmit.RemoveListener(new UnityAction<string>(this.AddToChatOutput));
    }

    // Token: 0x06000204 RID: 516 RVA: 0x0000A1A8 File Offset: 0x000083A8
    private void AddToChatOutput(string newText)
    {
        this.TMP_ChatInput.text = string.Empty;
        DateTime now = DateTime.Now;
        TMP_Text tmp_ChatOutput = this.TMP_ChatOutput;
        tmp_ChatOutput.text = string.Concat(new string[]
        {
            tmp_ChatOutput.text,
            "[<#FFFF80>",
            now.Hour.ToString("d2"),
            ":",
            now.Minute.ToString("d2"),
            ":",
            now.Second.ToString("d2"),
            "</color>] ",
            newText,
            "\n"
        });
        this.TMP_ChatInput.ActivateInputField();
        this.ChatScrollbar.value = 0f;
    }

    // Token: 0x0400028C RID: 652
    public TMP_InputField TMP_ChatInput;

    // Token: 0x0400028D RID: 653
    public TMP_Text TMP_ChatOutput;

    // Token: 0x0400028E RID: 654
    public Scrollbar ChatScrollbar;
}

using System;
using UnityEngine;

// Token: 0x02000004 RID: 4
public class CheckPoint : MonoBehaviour
{
    // Token: 0x06000006 RID: 6 RVA: 0x000020CF File Offset: 0x000002CF
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !this.alreadySaved)
        {
            this.alreadySaved = true;
            this.collector = other.transform.root.GetComponent<ItemCollector>();
            this.SaveData();
        }
    }

    // Token: 0x06000007 RID: 7 RVA: 0x00002109 File Offset: 0x00000309
    private void SaveData()
    {
        this.collector.SaveMushrooms();
        this.collector.GetComponent<PlayerController>().SavePosition();
        Object.FindObjectOfType<MenuExit>(true).SaveItemPresence();
        Debug.Log("CHECKPOINT");
    }

    // Token: 0x04000002 RID: 2
    public Transform point;

    // Token: 0x04000003 RID: 3
    private ItemCollector collector;

    // Token: 0x04000004 RID: 4
    private bool alreadySaved;
}

using System;
using UnityEngine;

// Token: 0x02000052 RID: 82
public static class CursorController
{
    // Token: 0x060001A0 RID: 416 RVA: 0x00008480 File Offset: 0x00006680
    public static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Token: 0x060001A1 RID: 417 RVA: 0x0000848E File Offset: 0x0000668E
    public static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

using System;
using UnityEngine;

// Token: 0x0200002D RID: 45
public class CurveSpawnedObject : MonoBehaviour
{
    // Token: 0x060000F7 RID: 247 RVA: 0x00005D33 File Offset: 0x00003F33
    private void OnDestroy()
    {
        this.curveSpawner.OnSpawnedObjectDestroy(this);
    }

    // Token: 0x04000152 RID: 338
    [HideInInspector]
    public CurveSpawner curveSpawner;
}

using System;
using System.Collections.Generic;
using UnityEngine;

// Token: 0x02000022 RID: 34
public class CurveSpawner : MonoBehaviour
{
    // Token: 0x17000004 RID: 4
    // (get) Token: 0x060000C9 RID: 201 RVA: 0x0000538D File Offset: 0x0000358D
    private float startSpawnTime
    {
        get
        {
            return this.decreaseCurve.Evaluate(this._time);
        }
    }

    // Token: 0x060000CA RID: 202 RVA: 0x000053A0 File Offset: 0x000035A0
    private void Start()
    {
        this.spawnTime = this.startSpawnTime;
    }

    // Token: 0x060000CB RID: 203 RVA: 0x000053B0 File Offset: 0x000035B0
    private void Update()
    {
        if (this.isSpawning)
        {
            this.showTime = this.startSpawnTime;
            this.spawnTime -= Time.deltaTime;
            this._time += Time.deltaTime;
            if (this.spawnTime <= 0f)
            {
                int num = Random.Range(0, this.spawnPoints.Length);
                int num2 = Random.Range(0, this.objectsToSpawn.Length);
                CurveSpawnedObject component = Object.Instantiate<GameObject>(this.objectsToSpawn[num2], this.spawnPoints[num].position, this.spawnPoints[num].rotation).GetComponent<CurveSpawnedObject>();
                component.curveSpawner = this;
                this.spawnedObjects.Add(component);
                this.spawnTime = this.startSpawnTime;
            }
            if (this._time >= this.totalTime)
            {
                this.isSpawning = false;
            }
        }
    }

    // Token: 0x060000CC RID: 204 RVA: 0x00005484 File Offset: 0x00003684
    public void OnSpawnedObjectDestroy(CurveSpawnedObject destroyedObject)
    {
        this.spawnedObjects.Remove(destroyedObject);
        if (this.spawnedObjects.Count == 0 && !this.isSpawning)
        {
            Action action = this.hasDoneSpawning;
            if (action != null)
            {
                action();
            }
            base.enabled = false;
        }
    }

    // Token: 0x060000CD RID: 205 RVA: 0x000054C0 File Offset: 0x000036C0
    public void OnEnable()
    {
        this._time = 0f;
        this.spawnTime = this.startSpawnTime;
        this.isSpawning = true;
    }

    // Token: 0x04000123 RID: 291
    public Transform[] spawnPoints;

    // Token: 0x04000124 RID: 292
    public GameObject[] objectsToSpawn;

    // Token: 0x04000125 RID: 293
    public AnimationCurve decreaseCurve;

    // Token: 0x04000126 RID: 294
    public Action hasDoneSpawning;

    // Token: 0x04000127 RID: 295
    private float _time;

    // Token: 0x04000128 RID: 296
    public float showTime;

    // Token: 0x04000129 RID: 297
    public float totalTime;

    // Token: 0x0400012A RID: 298
    public List<CurveSpawnedObject> spawnedObjects;

    // Token: 0x0400012B RID: 299
    private float spawnTime;

    // Token: 0x0400012C RID: 300
    private bool isSpawning;
}

using System;
using UnityEngine;

// Token: 0x02000063 RID: 99
public class CustomBullet : MonoBehaviour
{
    // Token: 0x060001E5 RID: 485 RVA: 0x00009A07 File Offset: 0x00007C07
    private void Start()
    {
        this.Setup();
    }

    // Token: 0x060001E6 RID: 486 RVA: 0x00009A0F File Offset: 0x00007C0F
    private void Update()
    {
        if (this.collisions > this.maxCollisions)
        {
            this.Explode();
        }
        this.maxLifetime -= Time.deltaTime;
        if (this.maxLifetime <= 0f)
        {
            this.Explode();
        }
    }

    // Token: 0x060001E7 RID: 487 RVA: 0x00009A4C File Offset: 0x00007C4C
    private void Explode()
    {
        if (this.explosion != null)
        {
            Object.Instantiate<GameObject>(this.explosion, base.transform.position, Quaternion.identity);
        }
        Collider[] array = Physics.OverlapSphere(base.transform.position, this.explosionRange, this.whatIsEnemies);
        for (int i = 0; i < array.Length; i++)
        {
            Enemy enemy;
            FoxShootable foxShootable;
            Melon melon;
            BooBoss booBoss;
            Candle candle;
            if (array[i].transform.root.TryGetComponent<Enemy>(out enemy))
            {
                enemy.TakeDamage(this.explosionDamage);
            }
            else if (array[i].TryGetComponent<FoxShootable>(out foxShootable))
            {
                foxShootable.TakeDamage(this.explosionDamage);
            }
            else if (array[i].TryGetComponent<Melon>(out melon))
            {
                melon.TakeDamage(this.explosionDamage);
            }
            else if (array[i].transform.root.TryGetComponent<BooBoss>(out booBoss))
            {
                if (!booBoss.forceField.activeInHierarchy)
                {
                    booBoss.TakeDamage(this.explosionDamage);
                }
            }
            else if (array[i].TryGetComponent<Candle>(out candle))
            {
                candle.BlowOutCandle();
            }
        }
        Object.Destroy(base.gameObject);
    }

    // Token: 0x060001E8 RID: 488 RVA: 0x00009B63 File Offset: 0x00007D63
    private void Delay()
    {
        Object.Destroy(base.gameObject);
    }

    // Token: 0x060001E9 RID: 489 RVA: 0x00009B70 File Offset: 0x00007D70
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Bullet"))
        {
            return;
        }
        this.collisions++;
        if (collision.collider.CompareTag("Enemy") && this.explodeOnTouch)
        {
            this.Explode();
        }
    }

    // Token: 0x060001EA RID: 490 RVA: 0x00009BC0 File Offset: 0x00007DC0
    private void Setup()
    {
        this.physics_mat = new PhysicMaterial();
        this.physics_mat.bounciness = this.bounciness;
        this.physics_mat.frictionCombine = PhysicMaterialCombine.Minimum;
        this.physics_mat.bounceCombine = PhysicMaterialCombine.Maximum;
        base.GetComponent<SphereCollider>().material = this.physics_mat;
        this.rb.useGravity = this.useGravity;
    }

    // Token: 0x060001EB RID: 491 RVA: 0x00009C23 File Offset: 0x00007E23
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.explosionRange);
    }

    // Token: 0x04000266 RID: 614
    public Rigidbody rb;

    // Token: 0x04000267 RID: 615
    public GameObject explosion;

    // Token: 0x04000268 RID: 616
    public LayerMask whatIsEnemies;

    // Token: 0x04000269 RID: 617
    [Range(0f, 1f)]
    public float bounciness;

    // Token: 0x0400026A RID: 618
    public bool useGravity;

    // Token: 0x0400026B RID: 619
    public int explosionDamage;

    // Token: 0x0400026C RID: 620
    public float explosionRange;

    // Token: 0x0400026D RID: 621
    public float explosionForce;

    // Token: 0x0400026E RID: 622
    public int maxCollisions;

    // Token: 0x0400026F RID: 623
    public float maxLifetime;

    // Token: 0x04000270 RID: 624
    public bool explodeOnTouch = true;

    // Token: 0x04000271 RID: 625
    private int collisions;

    // Token: 0x04000272 RID: 626
    private PhysicMaterial physics_mat;
}

using System;
using UnityEngine;

// Token: 0x02000053 RID: 83
public class CustomHint : MonoBehaviour
{
    // Token: 0x060001A2 RID: 418 RVA: 0x0000849C File Offset: 0x0000669C
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.hint.SetActive(true);
        }
    }

    // Token: 0x060001A3 RID: 419 RVA: 0x000084B7 File Offset: 0x000066B7
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.hint.SetActive(false);
        }
    }

    // Token: 0x060001A4 RID: 420 RVA: 0x000084D2 File Offset: 0x000066D2
    public void DeactivateTotally()
    {
        this.hint.SetActive(false);
        base.enabled = false;
    }

    // Token: 0x0400021F RID: 543
    public GameObject hint;
}

using System;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x02000005 RID: 5
public class CutoffMaskUI : Image
{
    // Token: 0x17000001 RID: 1
    // (get) Token: 0x06000009 RID: 9 RVA: 0x00002143 File Offset: 0x00000343
    public override Material materialForRendering
    {
        get
        {
            Material material = new Material(base.materialForRendering);
            material.SetInt("_StencilComp", 6);
            return material;
        }
    }
}

using System;
using UnityEngine;

// Token: 0x0200002E RID: 46
public class CutsceneChecker : MonoBehaviour
{
    // Token: 0x060000F9 RID: 249 RVA: 0x00005D4C File Offset: 0x00003F4C
    private void Start()
    {
        this._hasCompletedCutscene = PlayerPrefs.GetString("cutscene", "false");
        this._hasMetBooBoss = PlayerPrefs.GetString("hasMetBooBoss", "false");
        this._hasSeenTip = PlayerPrefs.GetString("hasSeenTip", "false");
        if (this._hasCompletedCutscene == "false")
        {
            this.player.SetActive(false);
            this.cutsceneObject.SetActive(true);
        }
        else if (this._hasCompletedCutscene == "true")
        {
            this.cutsceneObject.SetActive(false);
            this.player.SetActive(true);
        }
        this.boss.hasCompletedCutscene = (this._hasMetBooBoss == "true");
        this.boss.hasSeenTip = (this._hasSeenTip == "true");
    }

    // Token: 0x060000FA RID: 250 RVA: 0x00005E30 File Offset: 0x00004030
    public void CompleteCutscene()
    {
        this._hasCompletedCutscene = "true";
        PlayerPrefs.SetString("cutscene", "true");
    }

    // Token: 0x060000FB RID: 251 RVA: 0x00005E4C File Offset: 0x0000404C
    public void MeetBooBoss()
    {
        this._hasMetBooBoss = "true";
        PlayerPrefs.SetString("hasMetBooBoss", "true");
    }

    // Token: 0x060000FC RID: 252 RVA: 0x00005E68 File Offset: 0x00004068
    public void HasSeenTip()
    {
        this._hasSeenTip = "true";
        PlayerPrefs.SetString("hasSeenTip", "true");
    }

    // Token: 0x04000153 RID: 339
    [SerializeField]
    private GameObject cutsceneObject;

    // Token: 0x04000154 RID: 340
    [SerializeField]
    private GameObject player;

    // Token: 0x04000155 RID: 341
    [SerializeField]
    private BooBoss boss;

    // Token: 0x04000156 RID: 342
    private string _hasCompletedCutscene;

    // Token: 0x04000157 RID: 343
    private string _hasMetBooBoss;

    // Token: 0x04000158 RID: 344
    private string _hasSeenTip;
}

using System;
using UnityEngine;

// Token: 0x02000064 RID: 100
public class DamageDealer : MonoBehaviour
{
    // Token: 0x060001ED RID: 493 RVA: 0x00009C54 File Offset: 0x00007E54
    private void OnTriggerEnter(Collider other)
    {
        PlayerHealth playerHealth;
        if (other.transform.parent.TryGetComponent<PlayerHealth>(out playerHealth))
        {
            playerHealth.TakeDamage(this.damage);
        }
    }

    // Token: 0x060001EE RID: 494 RVA: 0x00009C84 File Offset: 0x00007E84
    private void OnParticleCollision(GameObject other)
    {
        PlayerHealth playerHealth;
        if (other.transform.parent.TryGetComponent<PlayerHealth>(out playerHealth))
        {
            playerHealth.TakeDamage(this.damage);
        }
    }

    // Token: 0x04000273 RID: 627
    public float damage;
}

using System;
using MEET_AND_TALK;
using TMPro;
using UnityEngine;

// Token: 0x02000085 RID: 133
[RequireComponent(typeof(CharacterController))]
public class DemoPlayer : MonoBehaviour
{
    // Token: 0x060002AC RID: 684 RVA: 0x0000CD49 File Offset: 0x0000AF49
    private void Start()
    {
        this.characterController = base.GetComponent<CharacterController>();
    }

    // Token: 0x060002AD RID: 685 RVA: 0x0000CD57 File Offset: 0x0000AF57
    public void SetupInteractable(bool mode)
    {
        this.Interactable = mode;
    }

    // Token: 0x060002AE RID: 686 RVA: 0x0000CD60 File Offset: 0x0000AF60
    private void Update()
    {
        if (this.Interactable)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Vector3 a = base.transform.TransformDirection(Vector3.forward);
            Vector3 a2 = base.transform.TransformDirection(Vector3.right);
            bool key = Input.GetKey(KeyCode.LeftShift);
            float d = this.canMove ? ((key ? this.runSpeed : this.walkSpeed) * Input.GetAxis("Vertical")) : 0f;
            float d2 = this.canMove ? ((key ? this.runSpeed : this.walkSpeed) * Input.GetAxis("Horizontal")) : 0f;
            float y = this.moveDirection.y;
            this.moveDirection = a * d + a2 * d2;
            if (Input.GetButton("Jump") && this.canMove && this.characterController.isGrounded)
            {
                this.moveDirection.y = this.jumpPower;
            }
            else
            {
                this.moveDirection.y = y;
            }
            if (!this.characterController.isGrounded)
            {
                this.moveDirection.y = this.moveDirection.y - this.gravity * Time.deltaTime;
            }
            this.characterController.Move(this.moveDirection * Time.deltaTime);
            if (this.canMove)
            {
                this.rotationX += -Input.GetAxis("Mouse Y") * this.lookSpeed;
                this.rotationX = Mathf.Clamp(this.rotationX, -this.lookXLimit, this.lookXLimit);
                this.playerCamera.transform.localRotation = Quaternion.Euler(this.rotationX, 0f, 0f);
                base.transform.rotation *= Quaternion.Euler(0f, Input.GetAxis("Mouse X") * this.lookSpeed, 0f);
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        this.Lanuage.text = string.Format("Language: {0}", this.localizationManager.SelectedLang());
        if (Input.GetKeyDown(KeyCode.U))
        {
            this.localizationManager.selectedLang = SystemLanguage.English;
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            this.localizationManager.selectedLang = SystemLanguage.Polish;
        }
    }

    // Token: 0x04000372 RID: 882
    public LocalizationManager localizationManager;

    // Token: 0x04000373 RID: 883
    public bool Interactable;

    // Token: 0x04000374 RID: 884
    public Camera playerCamera;

    // Token: 0x04000375 RID: 885
    public float walkSpeed = 6f;

    // Token: 0x04000376 RID: 886
    public float runSpeed = 12f;

    // Token: 0x04000377 RID: 887
    public float jumpPower = 7f;

    // Token: 0x04000378 RID: 888
    public float gravity = 10f;

    // Token: 0x04000379 RID: 889
    public TMP_Text Lanuage;

    // Token: 0x0400037A RID: 890
    public float lookSpeed = 2f;

    // Token: 0x0400037B RID: 891
    public float lookXLimit = 45f;

    // Token: 0x0400037C RID: 892
    private Vector3 moveDirection = Vector3.zero;

    // Token: 0x0400037D RID: 893
    private float rotationX;

    // Token: 0x0400037E RID: 894
    public bool canMove = true;

    // Token: 0x0400037F RID: 895
    private CharacterController characterController;
}

using System;
using UnityEngine;

// Token: 0x02000010 RID: 16
public class Destroyer : MonoBehaviour
{
    // Token: 0x0600006B RID: 107 RVA: 0x00003BE7 File Offset: 0x00001DE7
    private void Start()
    {
        Object.Destroy(base.gameObject, this.lifetime);
    }

    // Token: 0x04000096 RID: 150
    public float lifetime;
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// Token: 0x02000006 RID: 6
public class DialogueSwitch : MonoBehaviour
{
    // Token: 0x0600000B RID: 11 RVA: 0x00002164 File Offset: 0x00000364
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (this.firstTime)
            {
                this.dialogues[0].Invoke();
                this.firstTime = false;
                return;
            }
            if (this.collector.items < 15)
            {
                this.dialogues[1].Invoke();
                return;
            }
            if (this.collector.items >= 15)
            {
                this.dialogues[2].Invoke();
            }
        }
    }

    // Token: 0x0600000C RID: 12 RVA: 0x000021D4 File Offset: 0x000003D4
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.skip.Invoke();
        }
    }

    // Token: 0x04000005 RID: 5
    public bool firstTime = true;

    // Token: 0x04000006 RID: 6
    public ItemCollector collector;

    // Token: 0x04000007 RID: 7
    public UnityEvent[] dialogues;

    // Token: 0x04000008 RID: 8
    public UnityEvent skip;

    // Token: 0x04000009 RID: 9
    public Animator trollAnimator;

    // Token: 0x0400000A RID: 10
    public Animator fenceAnimator;

    // Token: 0x0400000B RID: 11
    public TextMeshPro tableText;

    // Token: 0x0400000C RID: 12
    public GameObject flower;

    // Token: 0x0400000D RID: 13
    public GameObject tip;
}

using System;
using UnityEngine;

// Token: 0x02000054 RID: 84
public class DomeMelonController : MonoBehaviour
{
    // Token: 0x060001A6 RID: 422 RVA: 0x000084F0 File Offset: 0x000066F0
    private void OnEnable()
    {
        MelonfallManager melonfallManager = this.melonfall;
        melonfallManager.hasCompletedMelonfall = (Action)Delegate.Combine(melonfallManager.hasCompletedMelonfall, new Action(this.OnMelonfallFinished));
        CurveSpawner curveSpawner = this.curveSpawner;
        curveSpawner.hasDoneSpawning = (Action)Delegate.Combine(curveSpawner.hasDoneSpawning, new Action(this.OnSpawnerFinished));
    }

    // Token: 0x060001A7 RID: 423 RVA: 0x0000854C File Offset: 0x0000674C
    private void OnDisable()
    {
        MelonfallManager melonfallManager = this.melonfall;
        melonfallManager.hasCompletedMelonfall = (Action)Delegate.Remove(melonfallManager.hasCompletedMelonfall, new Action(this.OnMelonfallFinished));
        CurveSpawner curveSpawner = this.curveSpawner;
        curveSpawner.hasDoneSpawning = (Action)Delegate.Remove(curveSpawner.hasDoneSpawning, new Action(this.OnSpawnerFinished));
    }

    // Token: 0x060001A8 RID: 424 RVA: 0x000085A7 File Offset: 0x000067A7
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !this._hasSteppedInCenter)
        {
            this._hasSteppedInCenter = true;
            this.floorGlassAnimator.SetTrigger("hasStepped");
            GravityController.instance.SetEarthGravity();
        }
    }

    // Token: 0x060001A9 RID: 425 RVA: 0x000085DF File Offset: 0x000067DF
    public void OnMelonfallFinished()
    {
        this.curveSpawner.enabled = true;
    }

    // Token: 0x060001AA RID: 426 RVA: 0x000085ED File Offset: 0x000067ED
    public void OnSpawnerFinished()
    {
        this.floorCylinderCollider.enabled = false;
        this.sign.SetActive(true);
        this.floorGlassAnimator.SetTrigger("hasFinished");
        MusicManager.instance.EnableMusic(0);
        base.enabled = false;
    }

    // Token: 0x04000220 RID: 544
    [SerializeField]
    private MelonfallManager melonfall;

    // Token: 0x04000221 RID: 545
    [SerializeField]
    private CurveSpawner curveSpawner;

    // Token: 0x04000222 RID: 546
    [SerializeField]
    private Collider floorCylinderCollider;

    // Token: 0x04000223 RID: 547
    [SerializeField]
    private Animator floorGlassAnimator;

    // Token: 0x04000224 RID: 548
    [SerializeField]
    private GameObject sign;

    // Token: 0x04000225 RID: 549
    private bool _hasSteppedInCenter;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x02000011 RID: 17
public class DoorInteraction : MonoBehaviour
{
    // Token: 0x0600006D RID: 109 RVA: 0x00003C02 File Offset: 0x00001E02
    private void Update()
    {
        this.playerNearDoor = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        if (this.playerNearDoor && Input.GetKeyDown(KeyCode.E))
        {
            this.OpenDoor();
        }
    }

    // Token: 0x0600006E RID: 110 RVA: 0x00003C44 File Offset: 0x00001E44
    public void OpenDoor()
    {
        this.doorAnimator.SetBool("isOpened", true);
        this.gateAnimator.SetBool("hasMushrooms", true);
        this.enemyRun.speed = 8f;
        this.enemyRun.GetComponent<AudioSource>().Play();
        Object.FindObjectOfType<MusicManager>().EnableMusic(3);
        base.enabled = false;
    }

    // Token: 0x0600006F RID: 111 RVA: 0x00003CA5 File Offset: 0x00001EA5
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x04000097 RID: 151
    public Animator doorAnimator;

    // Token: 0x04000098 RID: 152
    public Animator gateAnimator;

    // Token: 0x04000099 RID: 153
    public NavMeshAgent enemyRun;

    // Token: 0x0400009A RID: 154
    public LayerMask whatIsPlayer;

    // Token: 0x0400009B RID: 155
    public float sightRange;

    // Token: 0x0400009C RID: 156
    private bool playerNearDoor;
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Token: 0x0200007F RID: 127
public class DynamicJoystick : Joystick
{
    // Token: 0x17000010 RID: 16
    // (get) Token: 0x06000296 RID: 662 RVA: 0x0000CA64 File Offset: 0x0000AC64
    // (set) Token: 0x06000297 RID: 663 RVA: 0x0000CA6C File Offset: 0x0000AC6C
    public float MoveThreshold
    {
        get
        {
            return this.moveThreshold;
        }
        set
        {
            this.moveThreshold = Mathf.Abs(value);
        }
    }

    // Token: 0x06000298 RID: 664 RVA: 0x0000CA7A File Offset: 0x0000AC7A
    protected override void Start()
    {
        this.MoveThreshold = this.moveThreshold;
        base.Start();
        this.background.gameObject.SetActive(false);
    }

    // Token: 0x06000299 RID: 665 RVA: 0x0000CA9F File Offset: 0x0000AC9F
    public override void OnPointerDown(PointerEventData eventData)
    {
        this.background.anchoredPosition = base.ScreenPointToAnchoredPosition(eventData.position);
        this.background.gameObject.SetActive(true);
        base.OnPointerDown(eventData);
    }

    // Token: 0x0600029A RID: 666 RVA: 0x0000CAD0 File Offset: 0x0000ACD0
    public override void OnPointerUp(PointerEventData eventData)
    {
        this.background.gameObject.SetActive(false);
        base.OnPointerUp(eventData);
    }

    // Token: 0x0600029B RID: 667 RVA: 0x0000CAEC File Offset: 0x0000ACEC
    protected override void HandleInput(float magnitude, Vector2 normalised, Vector2 radius, Camera cam)
    {
        if (magnitude > this.moveThreshold)
        {
            Vector2 b = normalised * (magnitude - this.moveThreshold) * radius;
            this.background.anchoredPosition += b;
        }
        base.HandleInput(magnitude, normalised, radius, cam);
    }

    // Token: 0x04000369 RID: 873
    [SerializeField]
    private float moveThreshold = 1f;
}

using System;
using UnityEngine;

// Token: 0x02000084 RID: 132
[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class EnableDepthBuffer : MonoBehaviour
{
    // Token: 0x060002AA RID: 682 RVA: 0x0000CD0C File Offset: 0x0000AF0C
    private void Update()
    {
        if (this.m_camera == null)
        {
            this.m_camera = base.GetComponent<Camera>();
        }
        if (this.m_camera.depthTextureMode == DepthTextureMode.None)
        {
            this.m_camera.depthTextureMode = DepthTextureMode.Depth;
        }
    }

    // Token: 0x04000371 RID: 881
    private Camera m_camera;
}

using System;
using UnityEngine;

// Token: 0x02000012 RID: 18
public class EndGame : MonoBehaviour
{
    // Token: 0x06000071 RID: 113 RVA: 0x00003CD0 File Offset: 0x00001ED0
    public void FadeToTitles()
    {
        this.magicPickSound.Play();
        MusicManager.instance.EnableMusic(6);
        base.gameObject.SetActive(false);
        AfterTitles[] array = Object.FindObjectsOfType<AfterTitles>(true);
        for (int i = 0; i < array.Length; i++)
        {
            array[i].gameObject.SetActive(true);
        }
    }

    // Token: 0x0400009D RID: 157
    public AudioSource magicPickSound;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x0200000B RID: 11
public class Enemy : MonoBehaviour
{
    // Token: 0x06000046 RID: 70 RVA: 0x000031C9 File Offset: 0x000013C9
    private void Awake()
    {
        this.player = GameObject.FindGameObjectWithTag("Player").transform.root;
        this.agent = base.GetComponent<NavMeshAgent>();
    }

    // Token: 0x06000047 RID: 71 RVA: 0x000031F4 File Offset: 0x000013F4
    private void Update()
    {
        this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
        if (!this.playerInSightRange && !this.playerInAttackRange)
        {
            this.Patroling();
        }
        if (this.playerInSightRange && !this.playerInAttackRange)
        {
            this.ChasePlayer();
        }
        if (this.playerInAttackRange && this.playerInSightRange)
        {
            this.AttackPlayer();
        }
    }

    // Token: 0x06000048 RID: 72 RVA: 0x00003294 File Offset: 0x00001494
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x06000049 RID: 73 RVA: 0x000032F8 File Offset: 0x000014F8
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x0600004A RID: 74 RVA: 0x000033A0 File Offset: 0x000015A0
    private void ChasePlayer()
    {
        if (Vector3.Distance(this.player.position, this.lastPlayerPosition) > 0.5f)
        {
            this.agent.SetDestination(this.player.position);
            this.lastPlayerPosition = this.player.position;
        }
    }

    // Token: 0x0600004B RID: 75 RVA: 0x000033F4 File Offset: 0x000015F4
    private void AttackPlayer()
    {
        if (Vector3.Distance(this.player.position, this.lastPlayerPosition) > 0.5f)
        {
            this.agent.SetDestination(this.player.position);
            this.lastPlayerPosition = this.player.position;
        }
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x0600004C RID: 76 RVA: 0x000034BD File Offset: 0x000016BD
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x0600004D RID: 77 RVA: 0x000034C6 File Offset: 0x000016C6
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x0600004E RID: 78 RVA: 0x000034F4 File Offset: 0x000016F4
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x0600004F RID: 79 RVA: 0x00003520 File Offset: 0x00001720
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x0400005E RID: 94
    public NavMeshAgent agent;

    // Token: 0x0400005F RID: 95
    public Transform player;

    // Token: 0x04000060 RID: 96
    public LayerMask whatIsGround;

    // Token: 0x04000061 RID: 97
    public LayerMask whatIsPlayer;

    // Token: 0x04000062 RID: 98
    public Vector3 walkPoint;

    // Token: 0x04000063 RID: 99
    private bool walkPointSet;

    // Token: 0x04000064 RID: 100
    public float walkPointRange;

    // Token: 0x04000065 RID: 101
    public float timeBetweenAttacks;

    // Token: 0x04000066 RID: 102
    private bool alreadyAttacked;

    // Token: 0x04000067 RID: 103
    public float sightRange;

    // Token: 0x04000068 RID: 104
    public float attackRange;

    // Token: 0x04000069 RID: 105
    public bool playerInSightRange;

    // Token: 0x0400006A RID: 106
    public bool playerInAttackRange;

    // Token: 0x0400006B RID: 107
    public float health;

    // Token: 0x0400006C RID: 108
    public int damage;

    // Token: 0x0400006D RID: 109
    public GameObject deathEffect;

    // Token: 0x0400006E RID: 110
    private Vector3 lastPlayerPosition;
}

using System;
using UnityEngine;

// Token: 0x0200006C RID: 108
public class EnemyFast : MonoBehaviour
{
    // Token: 0x06000209 RID: 521 RVA: 0x0000A2B8 File Offset: 0x000084B8
    private void OnEnable()
    {
        this.health = this.maxHealth;
        base.CancelInvoke();
    }

    // Token: 0x0600020A RID: 522 RVA: 0x0000A2CC File Offset: 0x000084CC
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DisableEnemy", 0.5f);
        }
    }

    // Token: 0x0600020B RID: 523 RVA: 0x0000A2FA File Offset: 0x000084FA
    private void DisableEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        base.gameObject.SetActive(false);
    }

    // Token: 0x04000292 RID: 658
    public float health;

    // Token: 0x04000293 RID: 659
    public float maxHealth;

    // Token: 0x04000294 RID: 660
    public int damage;

    // Token: 0x04000295 RID: 661
    public GameObject deathEffect;
}

using System;
using UnityEngine;

// Token: 0x0200000C RID: 12
public class EnemySpawner : MonoBehaviour
{
    // Token: 0x06000051 RID: 81 RVA: 0x00003578 File Offset: 0x00001778
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            int num = Random.Range(0, this.spawnPoints.Length);
            int num2 = Random.Range(0, this.enemies.Length);
            Object.Instantiate<GameObject>(this.enemies[num2], this.spawnPoints[num].position, Quaternion.identity);
            base.enabled = false;
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x0400006F RID: 111
    public Transform[] spawnPoints;

    // Token: 0x04000070 RID: 112
    public GameObject[] enemies;
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Token: 0x0200006B RID: 107
public class EnvMapAnimator : MonoBehaviour
{
    // Token: 0x06000206 RID: 518 RVA: 0x0000A282 File Offset: 0x00008482
    private void Awake()
    {
        this.m_textMeshPro = base.GetComponent<TMP_Text>();
        this.m_material = this.m_textMeshPro.fontSharedMaterial;
    }

    // Token: 0x06000207 RID: 519 RVA: 0x0000A2A1 File Offset: 0x000084A1
    private IEnumerator Start()
    {
        Matrix4x4 matrix = default(Matrix4x4);
        for (; ; )
        {
            matrix.SetTRS(Vector3.zero, Quaternion.Euler(Time.time * this.RotationSpeeds.x, Time.time * this.RotationSpeeds.y, Time.time * this.RotationSpeeds.z), Vector3.one);
            this.m_material.SetMatrix("_EnvMatrix", matrix);
            yield return null;
        }
        yield break;
    }

    // Token: 0x0400028F RID: 655
    public Vector3 RotationSpeeds;

    // Token: 0x04000290 RID: 656
    private TMP_Text m_textMeshPro;

    // Token: 0x04000291 RID: 657
    private Material m_material;
}

using System;
using UnityEngine;

// Token: 0x0200006E RID: 110
public class FinalPlatform : MonoBehaviour
{
    // Token: 0x06000210 RID: 528 RVA: 0x0000A38C File Offset: 0x0000858C
    private void Start()
    {
        if (this.pointA == null || this.pointB == null)
        {
            Debug.LogError("MovingPlatform: Assign both Point A and Point B.");
            return;
        }
        base.transform.position = this.pointA.position;
        this.targetPosition = this.pointB.position;
    }

    // Token: 0x06000211 RID: 529 RVA: 0x0000A3E8 File Offset: 0x000085E8
    private void FixedUpdate()
    {
        if (this.playerOnPlatform && this.playerCollector != null && this.playerCollector.items >= 42 && !this.reachedTarget)
        {
            Vector3 position = Vector3.MoveTowards(base.transform.position, this.targetPosition, this.speed * Time.fixedDeltaTime);
            base.transform.position = position;
            if (Vector3.Distance(base.transform.position, this.targetPosition) < 0.01f)
            {
                this.reachedTarget = true;
            }
        }
    }

    // Token: 0x06000212 RID: 530 RVA: 0x0000A475 File Offset: 0x00008675
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            collision.transform.SetParent(base.transform);
            this.playerOnPlatform = true;
            this.playerCollector = collision.gameObject.GetComponent<ItemCollector>();
        }
    }

    // Token: 0x06000213 RID: 531 RVA: 0x0000A4B2 File Offset: 0x000086B2
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            collision.transform.SetParent(null);
            this.playerOnPlatform = false;
        }
    }

    // Token: 0x04000298 RID: 664
    [Header("Movement Points")]
    public Transform pointA;

    // Token: 0x04000299 RID: 665
    public Transform pointB;

    // Token: 0x0400029A RID: 666
    [Header("Movement Settings")]
    public float speed = 2f;

    // Token: 0x0400029B RID: 667
    private Vector3 targetPosition;

    // Token: 0x0400029C RID: 668
    private bool playerOnPlatform;

    // Token: 0x0400029D RID: 669
    public ItemCollector playerCollector;

    // Token: 0x0400029E RID: 670
    private bool reachedTarget;
}

using System;

// Token: 0x02000080 RID: 128
public class FixedJoystick : Joystick
{
}

using System;
using UnityEngine.EventSystems;

// Token: 0x02000081 RID: 129
public class FloatingJoystick : Joystick
{
    // Token: 0x0600029E RID: 670 RVA: 0x0000CB54 File Offset: 0x0000AD54
    protected override void Start()
    {
        base.Start();
        this.background.gameObject.SetActive(false);
    }

    // Token: 0x0600029F RID: 671 RVA: 0x0000CB6D File Offset: 0x0000AD6D
    public override void OnPointerDown(PointerEventData eventData)
    {
        this.background.anchoredPosition = base.ScreenPointToAnchoredPosition(eventData.position);
        this.background.gameObject.SetActive(true);
        base.OnPointerDown(eventData);
    }

    // Token: 0x060002A0 RID: 672 RVA: 0x0000CB9E File Offset: 0x0000AD9E
    public override void OnPointerUp(PointerEventData eventData)
    {
        this.background.gameObject.SetActive(false);
        base.OnPointerUp(eventData);
    }
}

using System;
using UnityEngine;

// Token: 0x02000023 RID: 35
public class FoodActivator : MonoBehaviour
{
    // Token: 0x060000CF RID: 207 RVA: 0x000054E8 File Offset: 0x000036E8
    private void OnDestroy()
    {
        this.burger.StartCoroutine(this.burger.OnBurgerDestroyed());
    }

    // Token: 0x0400012D RID: 301
    public BurgerBehaviour burger;
}

using System;
using UnityEngine;
using UnityEngine.Events;

// Token: 0x0200002F RID: 47
public class FoxDialogueSwitch : MonoBehaviour
{
    // Token: 0x060000FE RID: 254 RVA: 0x00005E8C File Offset: 0x0000408C
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (!this.tierController.HasWon)
            {
                this.dialogues[0].Invoke();
                this.SwitchGateState(false);
                return;
            }
            if (this.tierController.HasWon && this.itemCollector.items >= 10)
            {
                this.dialogues[1].Invoke();
                this.SwitchGateState(true);
            }
        }
    }

    // Token: 0x060000FF RID: 255 RVA: 0x00005EF8 File Offset: 0x000040F8
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            this.skip.Invoke();
        }
    }

    // Token: 0x06000100 RID: 256 RVA: 0x00005F0E File Offset: 0x0000410E
    public void SwitchGateState(bool state)
    {
        this.gateAnimator.SetBool("isOpened", state);
    }

    // Token: 0x04000159 RID: 345
    [SerializeField]
    private FoxTierController tierController;

    // Token: 0x0400015A RID: 346
    [SerializeField]
    private UnityEvent[] dialogues;

    // Token: 0x0400015B RID: 347
    [SerializeField]
    private UnityEvent skip;

    // Token: 0x0400015C RID: 348
    [SerializeField]
    private Animator gateAnimator;

    // Token: 0x0400015D RID: 349
    [SerializeField]
    private ItemCollector itemCollector;
}

using System;
using UnityEngine;

// Token: 0x02000030 RID: 48
public class FoxShootable : MonoBehaviour
{
    // Token: 0x06000102 RID: 258 RVA: 0x00005F29 File Offset: 0x00004129
    private void Start()
    {
        this._foxTierController = Object.FindObjectOfType<FoxTierController>();
    }

    // Token: 0x06000103 RID: 259 RVA: 0x00005F36 File Offset: 0x00004136
    public void TakeDamage(int damage)
    {
        this.health -= damage;
        if (this.health <= 0)
        {
            this._foxTierController.DestroyFox(base.gameObject);
        }
    }

    // Token: 0x0400015E RID: 350
    [SerializeField]
    private int health;

    // Token: 0x0400015F RID: 351
    public int FoxType;

    // Token: 0x04000160 RID: 352
    private FoxTierController _foxTierController;
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Token: 0x02000031 RID: 49
public class FoxTierController : MonoBehaviour
{
    // Token: 0x06000105 RID: 261 RVA: 0x00005F68 File Offset: 0x00004168
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.targetFoxType = 5;
            this.SpawnFoxes();
            this.hintText.text = "стреляйте только в указанных лисиц!";
            this.hintText.transform.root.GetComponent<Animator>().SetTrigger("isActivated");
            this.icon.SetActive(true);
        }
    }

    // Token: 0x06000106 RID: 262 RVA: 0x00005FCA File Offset: 0x000041CA
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.icon.SetActive(false);
        }
    }

    // Token: 0x06000107 RID: 263 RVA: 0x00005FE8 File Offset: 0x000041E8
    public void SpawnFoxes()
    {
        this._targetFoxCount = 0;
        this.DestroyAllFoxes();
        this.spawnedFoxes = new List<GameObject>();
        this.targetFoxType = ((this.targetFoxType < 5) ? (this.targetFoxType + 1) : 0);
        GameObject[] array = this.targetFoxIcons;
        for (int i = 0; i < array.Length; i++)
        {
            array[i].SetActive(false);
        }
        this.targetFoxIcons[this.targetFoxType].SetActive(true);
        foreach (Transform transform in this.foxPositions)
        {
            int num = Random.Range(0, this.foxVariants.Length);
            FoxShootable component = Object.Instantiate<GameObject>(this.foxVariants[num], transform.position, transform.rotation).GetComponent<FoxShootable>();
            component.FoxType = num;
            if (num == this.targetFoxType)
            {
                this._targetFoxCount++;
            }
            this.spawnedFoxes.Add(component.gameObject);
        }
        this.targetFoxCountText.text = "x" + this._targetFoxCount.ToString();
        if (this._targetFoxCount == 0)
        {
            this.targetFoxType = ((this.targetFoxType > 0) ? (this.targetFoxType - 1) : 5);
            this.SpawnFoxes();
        }
        if (this._targetFoxCount >= 7 && this.secretFox != null)
        {
            this.secretFox.SetActive(true);
        }
    }

    // Token: 0x06000108 RID: 264 RVA: 0x00006140 File Offset: 0x00004340
    public void DestroyAllFoxes()
    {
        foreach (GameObject obj in this.spawnedFoxes)
        {
            Object.Destroy(obj);
        }
    }

    // Token: 0x06000109 RID: 265 RVA: 0x00006190 File Offset: 0x00004390
    public void DestroyFox(GameObject fox)
    {
        if (fox.GetComponent<FoxShootable>().FoxType == this.targetFoxType)
        {
            if (this._targetFoxCount > 1)
            {
                this._targetFoxCount--;
                this.targetFoxCountText.text = "x" + this._targetFoxCount.ToString();
            }
            else if (this.targetFoxType == 5)
            {
                this.hintText.text = "победа!";
                this._targetFoxCount--;
                this.targetFoxCountText.text = "x" + this._targetFoxCount.ToString();
                this.hintText.transform.root.GetComponent<Animator>().SetTrigger("isActivated");
                this.HasWon = true;
                this.DestroyAllFoxes();
                this.icon.SetActive(false);
                base.GetComponent<Collider>().enabled = false;
            }
            else
            {
                this.SpawnFoxes();
            }
        }
        else
        {
            this.targetFoxType = 5;
            this.hintText.text = "мимо! Попробуйте снова";
            this.hintText.transform.root.GetComponent<Animator>().SetTrigger("isActivated");
            this.SpawnFoxes();
        }
        if (this.spawnedFoxes.Contains(fox))
        {
            Object.Destroy(fox);
            this.spawnedFoxes.Remove(fox);
        }
    }

    // Token: 0x04000161 RID: 353
    [SerializeField]
    private Transform[] foxPositions;

    // Token: 0x04000162 RID: 354
    [SerializeField]
    private GameObject[] foxVariants;

    // Token: 0x04000163 RID: 355
    [SerializeField]
    private GameObject[] targetFoxIcons;

    // Token: 0x04000164 RID: 356
    [SerializeField]
    private GameObject icon;

    // Token: 0x04000165 RID: 357
    [SerializeField]
    private List<GameObject> spawnedFoxes;

    // Token: 0x04000166 RID: 358
    [SerializeField]
    private int targetFoxType;

    // Token: 0x04000167 RID: 359
    [SerializeField]
    private TextMeshProUGUI targetFoxCountText;

    // Token: 0x04000168 RID: 360
    [SerializeField]
    private TextMeshProUGUI hintText;

    // Token: 0x04000169 RID: 361
    [SerializeField]
    private GameObject secretFox;

    // Token: 0x0400016A RID: 362
    public bool HasWon;

    // Token: 0x0400016B RID: 363
    private int _targetFoxCount;
}

using System;
using UnityEngine;

// Token: 0x02000032 RID: 50
public class FurnaceLighterTrigger : MonoBehaviour
{
    // Token: 0x0600010B RID: 267 RVA: 0x000062EC File Offset: 0x000044EC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this._isInZone = true;
            this.hints[this._hasLit ? 1 : 0].SetActive(true);
            this.customHint.DeactivateTotally();
        }
    }

    // Token: 0x0600010C RID: 268 RVA: 0x00006326 File Offset: 0x00004526
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this._isInZone = false;
            this.hints[this._hasLit ? 1 : 0].SetActive(false);
        }
    }

    // Token: 0x0600010D RID: 269 RVA: 0x00006358 File Offset: 0x00004558
    private void Update()
    {
        if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return)) && this._isInZone)
        {
            if (this.collector.items > 0 && !this._hasLit)
            {
                this._hasLit = true;
                this.hints[0].SetActive(false);
                this.collector.items = 0;
                this.woodPanel.transform.GetChild(0).gameObject.SetActive(false);
                this.collector.itemText.text = string.Format("{0}", this.collector.items);
                this.collector.SaveMushrooms();
                this.fire.SetActive(true);
                this.colliders[0].enabled = false;
                this.colliders[1].enabled = true;
                return;
            }
            if (this._hasLit && !this._hasSat)
            {
                this._hasSat = true;
                this.player.isStopped = true;
                this.player.gameObject.SetActive(false);
                this.cutscene.SetActive(true);
                this.hints[1].SetActive(false);
            }
        }
    }

    // Token: 0x0400016C RID: 364
    [SerializeField]
    private GameObject[] hints;

    // Token: 0x0400016D RID: 365
    [SerializeField]
    private Collider[] colliders;

    // Token: 0x0400016E RID: 366
    [SerializeField]
    private GameObject fire;

    // Token: 0x0400016F RID: 367
    [SerializeField]
    private GameObject woodPanel;

    // Token: 0x04000170 RID: 368
    [SerializeField]
    private GameObject cutscene;

    // Token: 0x04000171 RID: 369
    [SerializeField]
    private PlayerController player;

    // Token: 0x04000172 RID: 370
    [SerializeField]
    private ItemCollector collector;

    // Token: 0x04000173 RID: 371
    [SerializeField]
    private CustomHint customHint;

    // Token: 0x04000174 RID: 372
    private bool _isInZone;

    // Token: 0x04000175 RID: 373
    public bool _hasLit;

    // Token: 0x04000176 RID: 374
    public bool _hasSat;
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

// Token: 0x02000045 RID: 69
public class GameSettings : MonoBehaviour
{
    // Token: 0x06000168 RID: 360 RVA: 0x00007BAD File Offset: 0x00005DAD
    private void Start()
    {
        this.LoadValues();
    }

    // Token: 0x06000169 RID: 361 RVA: 0x00007BB5 File Offset: 0x00005DB5
    public void TextToSlider(float sensitivity)
    {
        this.sliderPercentUI.text = string.Format("чувствительность мыши: {0}%", Mathf.RoundToInt(sensitivity / 16f));
    }

    // Token: 0x0600016A RID: 362 RVA: 0x00007BDD File Offset: 0x00005DDD
    public void TickState()
    {
        if (this.postEffectsEnabled == 0)
        {
            this.tick.SetActive(false);
            this.postProcess.enabled = false;
            return;
        }
        this.tick.SetActive(true);
        this.postProcess.enabled = true;
    }

    // Token: 0x0600016B RID: 363 RVA: 0x00007C18 File Offset: 0x00005E18
    public void SaveSensitivity()
    {
        float value = this.sensitivitySlider.value;
        PlayerPrefs.SetFloat("SensitivityValue", value);
        this.LoadValues();
    }

    // Token: 0x0600016C RID: 364 RVA: 0x00007C44 File Offset: 0x00005E44
    public void SaveEffects()
    {
        if (this.tick.activeSelf)
        {
            this.tick.SetActive(false);
            this.postEffectsEnabled = 0;
        }
        else
        {
            this.tick.SetActive(true);
            this.postEffectsEnabled = 1;
        }
        PlayerPrefs.SetInt("PostEffects", this.postEffectsEnabled);
        this.LoadValues();
    }

    // Token: 0x0600016D RID: 365 RVA: 0x00007C9C File Offset: 0x00005E9C
    public void SaveQuality(int index)
    {
        this.graphicsQuality = index;
        PlayerPrefs.SetInt("GraphicsQuality", this.graphicsQuality);
    }

    // Token: 0x0600016E RID: 366 RVA: 0x00007CB8 File Offset: 0x00005EB8
    public void LoadValues()
    {
        float num = 400f;
        int num2 = 1;
        int qualityLevel = 2;
        if (PlayerPrefs.HasKey("SensitivityValue"))
        {
            num = PlayerPrefs.GetFloat("SensitivityValue");
        }
        if (PlayerPrefs.HasKey("PostEffects"))
        {
            num2 = PlayerPrefs.GetInt("PostEffects");
        }
        if (PlayerPrefs.HasKey("GraphicsQuality"))
        {
            qualityLevel = PlayerPrefs.GetInt("GraphicsQuality");
        }
        this.sensitivitySlider.value = num;
        this.postEffectsEnabled = num2;
        this.graphicsQuality = qualityLevel;
        this.qualityDropdown.SetQualityLevel(qualityLevel);
        this.TextToSlider(num);
        this.TickState();
        this.playerCamera.sensitivityX = (this.playerCamera.sensitivityY = num);
    }

    // Token: 0x040001F7 RID: 503
    [SerializeField]
    private Slider sensitivitySlider;

    // Token: 0x040001F8 RID: 504
    [SerializeField]
    private PostProcessLayer postProcess;

    // Token: 0x040001F9 RID: 505
    [SerializeField]
    private PlayerCamera playerCamera;

    // Token: 0x040001FA RID: 506
    [SerializeField]
    private TextMeshProUGUI sliderPercentUI;

    // Token: 0x040001FB RID: 507
    [SerializeField]
    private SetQuality qualityDropdown;

    // Token: 0x040001FC RID: 508
    public int postEffectsEnabled;

    // Token: 0x040001FD RID: 509
    public int graphicsQuality;

    // Token: 0x040001FE RID: 510
    [SerializeField]
    private GameObject tick;
}

using System;
using UnityEngine;

// Token: 0x02000024 RID: 36
public class GIFController : MonoBehaviour
{
    // Token: 0x060000D1 RID: 209 RVA: 0x00005509 File Offset: 0x00003709
    private void Start()
    {
    }

    // Token: 0x060000D2 RID: 210 RVA: 0x0000550B File Offset: 0x0000370B
    private void Update()
    {
    }
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Token: 0x02000016 RID: 22
public class GravityController : MonoBehaviour
{
    // Token: 0x0600007D RID: 125 RVA: 0x00003E81 File Offset: 0x00002081
    private void Awake()
    {
        if (GravityController.instance != null)
        {
            Object.Destroy(base.gameObject);
            return;
        }
        GravityController.instance = this;
        Object.DontDestroyOnLoad(base.transform.gameObject);
    }

    // Token: 0x0600007E RID: 126 RVA: 0x00003EB4 File Offset: 0x000020B4
    public void CheckGravity()
    {
        if (SceneManager.GetActiveScene().buildIndex == 2)
        {
            this.SetMoonGravity();
            return;
        }
        if (SceneManager.GetActiveScene().buildIndex == 1)
        {
            this.SetEarthGravity();
        }
    }

    // Token: 0x0600007F RID: 127 RVA: 0x00003EEE File Offset: 0x000020EE
    public void SetMoonGravity()
    {
        Physics.gravity = this.moonGravity;
        this.player.jumpForce = this.moonJumpForce;
    }

    // Token: 0x06000080 RID: 128 RVA: 0x00003F0C File Offset: 0x0000210C
    public void SetEarthGravity()
    {
        Physics.gravity = this.earthGravity;
        this.player.jumpForce = this.earthJumpForce;
    }

    // Token: 0x040000A7 RID: 167
    public static GravityController instance;

    // Token: 0x040000A8 RID: 168
    [SerializeField]
    private Vector3 moonGravity;

    // Token: 0x040000A9 RID: 169
    [SerializeField]
    private Vector3 earthGravity;

    // Token: 0x040000AA RID: 170
    public PlayerController player;

    // Token: 0x040000AB RID: 171
    [SerializeField]
    private float earthJumpForce;

    // Token: 0x040000AC RID: 172
    [SerializeField]
    private float moonJumpForce;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x02000013 RID: 19
public class GribmanActivator : MonoBehaviour
{
    // Token: 0x06000073 RID: 115 RVA: 0x00003D2A File Offset: 0x00001F2A
    private void Start()
    {
        Object.Instantiate<GameObject>(this.gribmanSound, base.transform.position, Quaternion.identity);
        base.Invoke("AttackPlayer", 8.5f);
    }

    // Token: 0x06000074 RID: 116 RVA: 0x00003D58 File Offset: 0x00001F58
    public void AttackPlayer()
    {
        base.GetComponent<NavMeshAgent>().speed = 10f;
        Object.Instantiate<GameObject>(this.attackSound, base.transform.position, Quaternion.identity);
        base.Invoke("AttackPlayer", 8.5f);
    }

    // Token: 0x0400009E RID: 158
    public GameObject gribmanSound;

    // Token: 0x0400009F RID: 159
    public GameObject attackSound;
}

using System;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x02000055 RID: 85
public class GridLayoutManager : MonoBehaviour
{
    // Token: 0x060001AC RID: 428 RVA: 0x00008631 File Offset: 0x00006831
    private void Start()
    {
        this.ConfigureGrid(2, 4, 150f, 150f, 10f, 10f);
    }

    // Token: 0x060001AD RID: 429 RVA: 0x00008650 File Offset: 0x00006850
    public void ConfigureGrid(int gridRows, int gridCols, float cellWidth, float cellHeight, float spacingX, float spacingY)
    {
        if (this.gridLayoutGroup == null)
        {
            Debug.LogError("GridLayoutGroup is not assigned!");
            return;
        }
        this.gridLayoutGroup.cellSize = new Vector2(cellWidth, cellHeight);
        this.gridLayoutGroup.spacing = new Vector2(spacingX, spacingY);
        float x = (float)gridCols * cellWidth + (float)(gridCols - 1) * spacingX;
        float y = (float)gridRows * cellHeight + (float)(gridRows - 1) * spacingY;
        this.cardParentRect.sizeDelta = new Vector2(x, y);
        this.gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        this.gridLayoutGroup.constraintCount = gridCols;
    }

    // Token: 0x04000226 RID: 550
    public GridLayoutGroup gridLayoutGroup;

    // Token: 0x04000227 RID: 551
    public int gridRows = 2;

    // Token: 0x04000228 RID: 552
    public int gridCols = 4;

    // Token: 0x04000229 RID: 553
    public float cellWidth = 150f;

    // Token: 0x0400022A RID: 554
    public float cellHeight = 150f;

    // Token: 0x0400022B RID: 555
    public float spacingX = 10f;

    // Token: 0x0400022C RID: 556
    public float spacingY = 10f;

    // Token: 0x0400022D RID: 557
    public RectTransform cardParentRect;
}

using System;
using UnityEngine;

// Token: 0x02000065 RID: 101
public class GunPickup : MonoBehaviour
{
    // Token: 0x060001F0 RID: 496 RVA: 0x00009CB9 File Offset: 0x00007EB9
    private void Start()
    {
        if (PlayerPrefs.GetString("Gun", "false") == "true")
        {
            base.gameObject.SetActive(false);
        }
    }

    // Token: 0x060001F1 RID: 497 RVA: 0x00009CE4 File Offset: 0x00007EE4
    public void PickupGun()
    {
        this.Gun.SetActive(true);
        this.Aim.SetActive(true);
        this.GunText.SetActive(true);
        if (this.Sound != null)
        {
            Object.Instantiate<GameObject>(this.Sound, base.transform.position, Quaternion.identity);
        }
        this.player.hasGun = "true";
        Object.Destroy(base.gameObject);
    }

    // Token: 0x04000274 RID: 628
    public GameObject Gun;

    // Token: 0x04000275 RID: 629
    public GameObject Aim;

    // Token: 0x04000276 RID: 630
    public GameObject GunText;

    // Token: 0x04000277 RID: 631
    public GameObject Sound;

    // Token: 0x04000278 RID: 632
    public PlayerController player;
}

using System;
using UnityEngine;

// Token: 0x02000056 RID: 86
public class HorrorSoundTrigger : MonoBehaviour
{
    // Token: 0x060001AF RID: 431 RVA: 0x00008731 File Offset: 0x00006931
    private void Start()
    {
    }

    // Token: 0x060001B0 RID: 432 RVA: 0x00008733 File Offset: 0x00006933
    private void Update()
    {
    }
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

// Token: 0x02000018 RID: 24
public class Intro : MonoBehaviour
{
    // Token: 0x0600008C RID: 140 RVA: 0x00004334 File Offset: 0x00002534
    private void Awake()
    {
        this.LoadIntro();
    }

    // Token: 0x0600008D RID: 141 RVA: 0x0000433C File Offset: 0x0000253C
    private void Start()
    {
        if (this.hasWatched == "false")
        {
            base.StartCoroutine(this.WatchVid());
            return;
        }
        this.videoClip.gameObject.SetActive(false);
    }

    // Token: 0x0600008E RID: 142 RVA: 0x0000436F File Offset: 0x0000256F
    private IEnumerator WatchVid()
    {
        PlayerController player = Object.FindObjectOfType<PlayerController>();
        player.isStopped = true;
        this.videoClip.gameObject.SetActive(true);
        this.videoClip.Play();
        yield return new WaitForSeconds(21.5f);
        this.hasWatched = "true";
        this.videoClip.gameObject.SetActive(false);
        player.isStopped = false;
        this.SaveIntro();
        yield break;
    }

    // Token: 0x0600008F RID: 143 RVA: 0x0000437E File Offset: 0x0000257E
    private void SaveIntro()
    {
        PlayerPrefs.SetString("Intro", this.hasWatched);
    }

    // Token: 0x06000090 RID: 144 RVA: 0x00004390 File Offset: 0x00002590
    private void LoadIntro()
    {
        this.hasWatched = PlayerPrefs.GetString("Intro", "false");
    }

    // Token: 0x040000C5 RID: 197
    public string hasWatched = "false";

    // Token: 0x040000C6 RID: 198
    public VideoPlayer videoClip;
}

using System;
using UnityEngine;

// Token: 0x0200001A RID: 26
public class Item : MonoBehaviour
{
    // Token: 0x06000095 RID: 149 RVA: 0x000043C6 File Offset: 0x000025C6
    private void Start()
    {
        if (!this.isFinalMushroom)
        {
            this.LoadPresence();
        }
    }

    // Token: 0x06000096 RID: 150 RVA: 0x000043D8 File Offset: 0x000025D8
    public void OnCollect()
    {
        this.isCollected = "yes";
        if (this.pickSound != null)
        {
            Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
        }
        if (this.isFinalMushroom)
        {
            base.GetComponent<ShaiActivator>().OnPick();
        }
        base.gameObject.SetActive(false);
    }

    // Token: 0x06000097 RID: 151 RVA: 0x00004439 File Offset: 0x00002639
    public void SavePresence()
    {
        PlayerPrefs.SetString("isCollected" + base.gameObject.name, this.isCollected);
    }

    // Token: 0x06000098 RID: 152 RVA: 0x0000445B File Offset: 0x0000265B
    public void LoadPresence()
    {
        this.isCollected = PlayerPrefs.GetString("isCollected" + base.gameObject.name);
        this.CheckPresence();
    }

    // Token: 0x06000099 RID: 153 RVA: 0x00004483 File Offset: 0x00002683
    public void CheckPresence()
    {
        if (this.isCollected == "yes")
        {
            base.gameObject.SetActive(false);
        }
    }

    // Token: 0x040000C7 RID: 199
    public string isCollected;

    // Token: 0x040000C8 RID: 200
    public GameObject pickSound;

    // Token: 0x040000C9 RID: 201
    public int count = 1;

    // Token: 0x040000CA RID: 202
    public bool isFinalMushroom;

    // Token: 0x040000CB RID: 203
    public bool test;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x0200001B RID: 27
public class ItemCollector : MonoBehaviour
{
    // Token: 0x0600009B RID: 155 RVA: 0x000044B2 File Offset: 0x000026B2
    private void Start()
    {
        this.LoadMushrooms();
    }

    // Token: 0x0600009C RID: 156 RVA: 0x000044BC File Offset: 0x000026BC
    private void Update()
    {
        RaycastHit raycastHit;
        if (Input.GetKeyDown(KeyCode.E) && Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out raycastHit, this.pickupRange, this.itemMask))
        {
            Item item;
            if (raycastHit.collider.TryGetComponent<Item>(out item))
            {
                this.items += item.count;
                this.itemText.text = (this.items.ToString() ?? "");
                raycastHit.collider.GetComponent<Item>().OnCollect();
                Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                Action action = this.hasCollected;
                if (action == null)
                {
                    return;
                }
                action();
                return;
            }
            else
            {
                if (raycastHit.collider.CompareTag("Flower"))
                {
                    base.GetComponent<PlayerController>().moveSpeed = 8f;
                    Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                    Object.Destroy(raycastHit.collider.gameObject);
                    return;
                }
                if (raycastHit.collider.CompareTag("Wood"))
                {
                    Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                    this.itemText.transform.parent.gameObject.SetActive(true);
                    this.items++;
                    this.itemText.text = (this.items.ToString() ?? "");
                    Object.Destroy(raycastHit.collider.gameObject);
                    return;
                }
                if (raycastHit.collider.CompareTag("Cotli"))
                {
                    base.GetComponent<PlayerController>().hasKotli = "true";
                    Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                    Object.Destroy(raycastHit.collider.gameObject);
                    return;
                }
                GunPickup gunPickup;
                if (raycastHit.collider.TryGetComponent<GunPickup>(out gunPickup))
                {
                    Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                    gunPickup.PickupGun();
                    return;
                }
                MemoryGameTrigger memoryGameTrigger;
                if (raycastHit.collider.TryGetComponent<MemoryGameTrigger>(out memoryGameTrigger))
                {
                    memoryGameTrigger.ActivateGame();
                    return;
                }
                if (raycastHit.collider.GetComponent<EndGame>() != null)
                {
                    Object.Instantiate<GameObject>(this.pickSound, base.transform.position, Quaternion.identity);
                    raycastHit.collider.GetComponent<EndGame>().FadeToTitles();
                }
            }
        }
    }

    // Token: 0x0600009D RID: 157 RVA: 0x00004737 File Offset: 0x00002937
    public void SaveMushrooms()
    {
        PlayerPrefs.SetInt("mushrooms", this.items);
    }

    // Token: 0x0600009E RID: 158 RVA: 0x0000474C File Offset: 0x0000294C
    public void LoadMushrooms()
    {
        this.items = PlayerPrefs.GetInt("mushrooms", 0);
        this.itemText.text = (this.items.ToString() ?? "");
        Action action = this.hasCollected;
        if (action == null)
        {
            return;
        }
        action();
    }

    // Token: 0x040000CC RID: 204
    public Action hasCollected;

    // Token: 0x040000CD RID: 205
    public TextMeshProUGUI itemText;

    // Token: 0x040000CE RID: 206
    public LayerMask itemMask;

    // Token: 0x040000CF RID: 207
    public GameObject pickSound;

    // Token: 0x040000D0 RID: 208
    public int items;

    // Token: 0x040000D1 RID: 209
    public float pickupRange;
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Token: 0x0200007D RID: 125
public class Joystick : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IDragHandler, IPointerUpHandler
{
    // Token: 0x17000008 RID: 8
    // (get) Token: 0x06000280 RID: 640 RVA: 0x0000C64B File Offset: 0x0000A84B
    public float Horizontal
    {
        get
        {
            if (!this.snapX)
            {
                return this.input.x;
            }
            return this.SnapFloat(this.input.x, AxisOptions.Horizontal);
        }
    }

    // Token: 0x17000009 RID: 9
    // (get) Token: 0x06000281 RID: 641 RVA: 0x0000C673 File Offset: 0x0000A873
    public float Vertical
    {
        get
        {
            if (!this.snapY)
            {
                return this.input.y;
            }
            return this.SnapFloat(this.input.y, AxisOptions.Vertical);
        }
    }

    // Token: 0x1700000A RID: 10
    // (get) Token: 0x06000282 RID: 642 RVA: 0x0000C69B File Offset: 0x0000A89B
    public Vector2 Direction
    {
        get
        {
            return new Vector2(this.Horizontal, this.Vertical);
        }
    }

    // Token: 0x1700000B RID: 11
    // (get) Token: 0x06000283 RID: 643 RVA: 0x0000C6AE File Offset: 0x0000A8AE
    // (set) Token: 0x06000284 RID: 644 RVA: 0x0000C6B6 File Offset: 0x0000A8B6
    public float HandleRange
    {
        get
        {
            return this.handleRange;
        }
        set
        {
            this.handleRange = Mathf.Abs(value);
        }
    }

    // Token: 0x1700000C RID: 12
    // (get) Token: 0x06000285 RID: 645 RVA: 0x0000C6C4 File Offset: 0x0000A8C4
    // (set) Token: 0x06000286 RID: 646 RVA: 0x0000C6CC File Offset: 0x0000A8CC
    public float DeadZone
    {
        get
        {
            return this.deadZone;
        }
        set
        {
            this.deadZone = Mathf.Abs(value);
        }
    }

    // Token: 0x1700000D RID: 13
    // (get) Token: 0x06000287 RID: 647 RVA: 0x0000C6DA File Offset: 0x0000A8DA
    // (set) Token: 0x06000288 RID: 648 RVA: 0x0000C6E2 File Offset: 0x0000A8E2
    public AxisOptions AxisOptions
    {
        get
        {
            return this.AxisOptions;
        }
        set
        {
            this.axisOptions = value;
        }
    }

    // Token: 0x1700000E RID: 14
    // (get) Token: 0x06000289 RID: 649 RVA: 0x0000C6EB File Offset: 0x0000A8EB
    // (set) Token: 0x0600028A RID: 650 RVA: 0x0000C6F3 File Offset: 0x0000A8F3
    public bool SnapX
    {
        get
        {
            return this.snapX;
        }
        set
        {
            this.snapX = value;
        }
    }

    // Token: 0x1700000F RID: 15
    // (get) Token: 0x0600028B RID: 651 RVA: 0x0000C6FC File Offset: 0x0000A8FC
    // (set) Token: 0x0600028C RID: 652 RVA: 0x0000C704 File Offset: 0x0000A904
    public bool SnapY
    {
        get
        {
            return this.snapY;
        }
        set
        {
            this.snapY = value;
        }
    }

    // Token: 0x0600028D RID: 653 RVA: 0x0000C710 File Offset: 0x0000A910
    protected virtual void Start()
    {
        this.HandleRange = this.handleRange;
        this.DeadZone = this.deadZone;
        this.baseRect = base.GetComponent<RectTransform>();
        this.canvas = base.GetComponentInParent<Canvas>();
        if (this.canvas == null)
        {
            Debug.LogError("The Joystick is not placed inside a canvas");
        }
        Vector2 vector = new Vector2(0.5f, 0.5f);
        this.background.pivot = vector;
        this.handle.anchorMin = vector;
        this.handle.anchorMax = vector;
        this.handle.pivot = vector;
        this.handle.anchoredPosition = Vector2.zero;
    }

    // Token: 0x0600028E RID: 654 RVA: 0x0000C7B6 File Offset: 0x0000A9B6
    public virtual void OnPointerDown(PointerEventData eventData)
    {
        this.OnDrag(eventData);
    }

    // Token: 0x0600028F RID: 655 RVA: 0x0000C7C0 File Offset: 0x0000A9C0
    public void OnDrag(PointerEventData eventData)
    {
        this.cam = null;
        if (this.canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            this.cam = this.canvas.worldCamera;
        }
        Vector2 b = RectTransformUtility.WorldToScreenPoint(this.cam, this.background.position);
        Vector2 vector = this.background.sizeDelta / 2f;
        this.input = (eventData.position - b) / (vector * this.canvas.scaleFactor);
        this.FormatInput();
        this.HandleInput(this.input.magnitude, this.input.normalized, vector, this.cam);
        this.handle.anchoredPosition = this.input * vector * this.handleRange;
    }

    // Token: 0x06000290 RID: 656 RVA: 0x0000C893 File Offset: 0x0000AA93
    protected virtual void HandleInput(float magnitude, Vector2 normalised, Vector2 radius, Camera cam)
    {
        if (magnitude > this.deadZone)
        {
            if (magnitude > 1f)
            {
                this.input = normalised;
                return;
            }
        }
        else
        {
            this.input = Vector2.zero;
        }
    }

    // Token: 0x06000291 RID: 657 RVA: 0x0000C8BC File Offset: 0x0000AABC
    private void FormatInput()
    {
        if (this.axisOptions == AxisOptions.Horizontal)
        {
            this.input = new Vector2(this.input.x, 0f);
            return;
        }
        if (this.axisOptions == AxisOptions.Vertical)
        {
            this.input = new Vector2(0f, this.input.y);
        }
    }

    // Token: 0x06000292 RID: 658 RVA: 0x0000C914 File Offset: 0x0000AB14
    private float SnapFloat(float value, AxisOptions snapAxis)
    {
        if (value == 0f)
        {
            return value;
        }
        if (this.axisOptions == AxisOptions.Both)
        {
            float num = Vector2.Angle(this.input, Vector2.up);
            if (snapAxis == AxisOptions.Horizontal)
            {
                if (num < 22.5f || num > 157.5f)
                {
                    return 0f;
                }
                return (float)((value > 0f) ? 1 : -1);
            }
            else
            {
                if (snapAxis != AxisOptions.Vertical)
                {
                    return value;
                }
                if (num > 67.5f && num < 112.5f)
                {
                    return 0f;
                }
                return (float)((value > 0f) ? 1 : -1);
            }
        }
        else
        {
            if (value > 0f)
            {
                return 1f;
            }
            if (value < 0f)
            {
                return -1f;
            }
            return 0f;
        }
    }

    // Token: 0x06000293 RID: 659 RVA: 0x0000C9B7 File Offset: 0x0000ABB7
    public virtual void OnPointerUp(PointerEventData eventData)
    {
        this.input = Vector2.zero;
        this.handle.anchoredPosition = Vector2.zero;
    }

    // Token: 0x06000294 RID: 660 RVA: 0x0000C9D4 File Offset: 0x0000ABD4
    protected Vector2 ScreenPointToAnchoredPosition(Vector2 screenPosition)
    {
        Vector2 zero = Vector2.zero;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(this.baseRect, screenPosition, this.cam, out zero))
        {
            Vector2 b = this.baseRect.pivot * this.baseRect.sizeDelta;
            return zero - this.background.anchorMax * this.baseRect.sizeDelta + b;
        }
        return Vector2.zero;
    }

    // Token: 0x0400035A RID: 858
    [SerializeField]
    private float handleRange = 1f;

    // Token: 0x0400035B RID: 859
    [SerializeField]
    private float deadZone;

    // Token: 0x0400035C RID: 860
    [SerializeField]
    private AxisOptions axisOptions;

    // Token: 0x0400035D RID: 861
    [SerializeField]
    private bool snapX;

    // Token: 0x0400035E RID: 862
    [SerializeField]
    private bool snapY;

    // Token: 0x0400035F RID: 863
    [SerializeField]
    protected RectTransform background;

    // Token: 0x04000360 RID: 864
    [SerializeField]
    private RectTransform handle;

    // Token: 0x04000361 RID: 865
    private RectTransform baseRect;

    // Token: 0x04000362 RID: 866
    private Canvas canvas;

    // Token: 0x04000363 RID: 867
    private Camera cam;

    // Token: 0x04000364 RID: 868
    private Vector2 input = Vector2.zero;
}

using System;
using UnityEngine;

// Token: 0x0200007B RID: 123
public class JoystickPlayerExample : MonoBehaviour
{
    // Token: 0x06000278 RID: 632 RVA: 0x0000C4C8 File Offset: 0x0000A6C8
    public void FixedUpdate()
    {
        Vector3 a = Vector3.forward * this.variableJoystick.Vertical + Vector3.right * this.variableJoystick.Horizontal;
        this.rb.AddForce(a * this.speed * Time.fixedDeltaTime, ForceMode.VelocityChange);
    }

    // Token: 0x04000353 RID: 851
    public float speed;

    // Token: 0x04000354 RID: 852
    public VariableJoystick variableJoystick;

    // Token: 0x04000355 RID: 853
    public Rigidbody rb;
}

using System;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x0200007C RID: 124
public class JoystickSetterExample : MonoBehaviour
{
    // Token: 0x0600027A RID: 634 RVA: 0x0000C52F File Offset: 0x0000A72F
    public void ModeChanged(int index)
    {
        switch (index)
        {
            case 0:
                this.variableJoystick.SetMode(JoystickType.Fixed);
                return;
            case 1:
                this.variableJoystick.SetMode(JoystickType.Floating);
                return;
            case 2:
                this.variableJoystick.SetMode(JoystickType.Dynamic);
                return;
            default:
                return;
        }
    }

    // Token: 0x0600027B RID: 635 RVA: 0x0000C56C File Offset: 0x0000A76C
    public void AxisChanged(int index)
    {
        switch (index)
        {
            case 0:
                this.variableJoystick.AxisOptions = AxisOptions.Both;
                this.background.sprite = this.axisSprites[index];
                return;
            case 1:
                this.variableJoystick.AxisOptions = AxisOptions.Horizontal;
                this.background.sprite = this.axisSprites[index];
                return;
            case 2:
                this.variableJoystick.AxisOptions = AxisOptions.Vertical;
                this.background.sprite = this.axisSprites[index];
                return;
            default:
                return;
        }
    }

    // Token: 0x0600027C RID: 636 RVA: 0x0000C5EB File Offset: 0x0000A7EB
    public void SnapX(bool value)
    {
        this.variableJoystick.SnapX = value;
    }

    // Token: 0x0600027D RID: 637 RVA: 0x0000C5F9 File Offset: 0x0000A7F9
    public void SnapY(bool value)
    {
        this.variableJoystick.SnapY = value;
    }

    // Token: 0x0600027E RID: 638 RVA: 0x0000C608 File Offset: 0x0000A808
    private void Update()
    {
        this.valueText.text = "Current Value: " + this.variableJoystick.Direction.ToString();
    }

    // Token: 0x04000356 RID: 854
    public VariableJoystick variableJoystick;

    // Token: 0x04000357 RID: 855
    public Text valueText;

    // Token: 0x04000358 RID: 856
    public Image background;

    // Token: 0x04000359 RID: 857
    public Sprite[] axisSprites;
}

using System;

// Token: 0x02000083 RID: 131
public enum JoystickType
{
    // Token: 0x0400036E RID: 878
    Fixed,
    // Token: 0x0400036F RID: 879
    Floating,
    // Token: 0x04000370 RID: 880
    Dynamic
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Token: 0x0200006F RID: 111
public class KotliSwitch : MonoBehaviour
{
    // Token: 0x06000215 RID: 533 RVA: 0x0000A4EC File Offset: 0x000086EC
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (this.playerController.hasKotli == "false" && !this.hasGivenKotli)
            {
                this.dialogues[0].Invoke();
                return;
            }
            if (this.playerController.hasKotli == "true" && !this.hasGivenKotli)
            {
                base.StartCoroutine(this.GiveSable());
                return;
            }
            if (this.hasGivenKotli)
            {
                this.dialogues[1].Invoke();
            }
        }
    }

    // Token: 0x06000216 RID: 534 RVA: 0x0000A575 File Offset: 0x00008775
    public IEnumerator GiveSable()
    {
        this.playerController.isStopped = true;
        this.audioSource.Play();
        yield return new WaitForSeconds(11f);
        this.playerController.hasGun = "true";
        this.playerController.hasKotli = "false";
        PlayerPrefs.SetString("Gun", "true");
        this.sable.SetActive(false);
        this.playerSable.SetActive(true);
        this.hasGivenKotli = true;
        this.playerController.isStopped = false;
        yield break;
    }

    // Token: 0x06000217 RID: 535 RVA: 0x0000A584 File Offset: 0x00008784
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.skip.Invoke();
        }
    }

    // Token: 0x0400029F RID: 671
    public UnityEvent[] dialogues;

    // Token: 0x040002A0 RID: 672
    public UnityEvent skip;

    // Token: 0x040002A1 RID: 673
    public GameObject sable;

    // Token: 0x040002A2 RID: 674
    public GameObject playerSable;

    // Token: 0x040002A3 RID: 675
    public PlayerController playerController;

    // Token: 0x040002A4 RID: 676
    public bool hasGivenKotli;

    // Token: 0x040002A5 RID: 677
    public AudioSource audioSource;
}

using System;
using UnityEngine;

// Token: 0x02000019 RID: 25
public class LevelSwitcher : MonoBehaviour
{
    // Token: 0x06000092 RID: 146 RVA: 0x000043BA File Offset: 0x000025BA
    private void Start()
    {
    }

    // Token: 0x06000093 RID: 147 RVA: 0x000043BC File Offset: 0x000025BC
    private void Update()
    {
    }
}

using System;
using UnityEngine;

// Token: 0x02000037 RID: 55
public class LightSwitcher : MonoBehaviour
{
    // Token: 0x0600012C RID: 300 RVA: 0x00006E15 File Offset: 0x00005015
    private void Update()
    {
        if (Input.GetAxis("Mouse ScrollWheel") != 0f || Input.GetKeyDown(KeyCode.Q))
        {
            this.SwitchLight();
        }
    }

    // Token: 0x0600012D RID: 301 RVA: 0x00006E38 File Offset: 0x00005038
    private void SwitchLight()
    {
        if (!this.flashlight.activeInHierarchy)
        {
            if (this.oilLamp != null)
            {
                this.oilLamp.SetActive(false);
            }
            this.flashlight.SetActive(true);
            return;
        }
        if (this.flashlight.activeInHierarchy)
        {
            this.flashlight.SetActive(false);
            if (this.oilLamp != null)
            {
                this.oilLamp.SetActive(true);
            }
        }
    }

    // Token: 0x040001A6 RID: 422
    public GameObject flashlight;

    // Token: 0x040001A7 RID: 423
    public GameObject oilLamp;
}

using System;

// Token: 0x02000087 RID: 135
public enum LocalizationEnum
{
    // Token: 0x04000383 RID: 899
    English,
    // Token: 0x04000384 RID: 900
    Polish
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000025 RID: 37
public class LockUI : MonoBehaviour
{
    // Token: 0x060000D4 RID: 212 RVA: 0x00005515 File Offset: 0x00003715
    private void Awake()
    {
        this.LoadPassword();
    }

    // Token: 0x060000D5 RID: 213 RVA: 0x0000551D File Offset: 0x0000371D
    public void SwitchPanelState(bool state)
    {
        this.lockPanel.SetActive(state);
    }

    // Token: 0x060000D6 RID: 214 RVA: 0x0000552C File Offset: 0x0000372C
    public void CheckPassword()
    {
        if (this.inputField.text.ToLower() == this.password)
        {
            this.checker.enabled = true;
            this.hintText.SetActive(false);
            this.inputField.gameObject.SetActive(false);
            this.rewardMessage.SetActive(true);
            base.gameObject.SetActive(false);
            this.enteredPassword = this.password;
            this.SavePassword();
        }
    }

    // Token: 0x060000D7 RID: 215 RVA: 0x000055A9 File Offset: 0x000037A9
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.lockPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
        }
    }

    // Token: 0x060000D8 RID: 216 RVA: 0x000055DA File Offset: 0x000037DA
    public void OnFoodKilled()
    {
        this.foodKilled = "true";
        this.SavePassword();
        this.Activate();
    }

    // Token: 0x060000D9 RID: 217 RVA: 0x000055F4 File Offset: 0x000037F4
    private void LoadPassword()
    {
        this.enteredPassword = PlayerPrefs.GetString("Password", "");
        this.foodKilled = PlayerPrefs.GetString("FoodKilled", "");
        if (this.enteredPassword == "offscreen")
        {
            this.checker.enabled = true;
            base.gameObject.SetActive(false);
            return;
        }
        if (this.foodKilled == "true")
        {
            this.Activate();
        }
    }

    // Token: 0x060000DA RID: 218 RVA: 0x0000566E File Offset: 0x0000386E
    private void SavePassword()
    {
        PlayerPrefs.SetString("Password", this.enteredPassword);
        PlayerPrefs.SetString("FoodKilled", this.foodKilled);
    }

    // Token: 0x060000DB RID: 219 RVA: 0x00005690 File Offset: 0x00003890
    public void Activate()
    {
        base.GetComponent<Outline>().enabled = true;
        base.GetComponent<SphereCollider>().enabled = true;
    }

    // Token: 0x0400012E RID: 302
    [SerializeField]
    private GameObject lockPanel;

    // Token: 0x0400012F RID: 303
    [SerializeField]
    private GameObject rewardMessage;

    // Token: 0x04000130 RID: 304
    [SerializeField]
    private GameObject hintText;

    // Token: 0x04000131 RID: 305
    [SerializeField]
    private TMP_InputField inputField;

    // Token: 0x04000132 RID: 306
    [SerializeField]
    private MushroomChecker checker;

    // Token: 0x04000133 RID: 307
    [SerializeField]
    private string password;

    // Token: 0x04000134 RID: 308
    private string foodKilled = "";

    // Token: 0x04000135 RID: 309
    private string enteredPassword = "";
}

using System;
using UnityEngine;

// Token: 0x02000057 RID: 87
public class Melon : MonoBehaviour
{
    // Token: 0x060001B2 RID: 434 RVA: 0x0000873D File Offset: 0x0000693D
    private void Start()
    {
        this._rigidbody = base.GetComponent<Rigidbody>();
    }

    // Token: 0x060001B3 RID: 435 RVA: 0x0000874B File Offset: 0x0000694B
    private void FixedUpdate()
    {
        this._rigidbody.AddForce(base.transform.forward * this.moveSpeed, ForceMode.Force);
    }

    // Token: 0x060001B4 RID: 436 RVA: 0x0000876F File Offset: 0x0000696F
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            this.DestroyMelon();
        }
    }

    // Token: 0x060001B5 RID: 437 RVA: 0x00008793 File Offset: 0x00006993
    public void DestroyMelon()
    {
        Object.Instantiate<GameObject>(this.destroyEffect, base.transform.position, Quaternion.identity);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x060001B6 RID: 438 RVA: 0x000087BC File Offset: 0x000069BC
    private void OnCollisionEnter(Collision other)
    {
        PlayerHealth playerHealth;
        if (other.gameObject.CompareTag("Player") && other.gameObject.TryGetComponent<PlayerHealth>(out playerHealth))
        {
            playerHealth.TakeDamage(this.damage);
            this.DestroyMelon();
        }
    }

    // Token: 0x0400022E RID: 558
    public float moveSpeed;

    // Token: 0x0400022F RID: 559
    public float health;

    // Token: 0x04000230 RID: 560
    public float damage;

    // Token: 0x04000231 RID: 561
    public GameObject destroyEffect;

    // Token: 0x04000232 RID: 562
    private Rigidbody _rigidbody;
}

using System;
using UnityEngine;

// Token: 0x02000058 RID: 88
public class MelonExitTrigger : MonoBehaviour
{
    // Token: 0x060001B8 RID: 440 RVA: 0x00008804 File Offset: 0x00006A04
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.hitboxes.SetActive(false);
            this.cylinder.SetActive(true);
        }
    }

    // Token: 0x04000233 RID: 563
    public GameObject hitboxes;

    // Token: 0x04000234 RID: 564
    public GameObject cylinder;
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Token: 0x02000059 RID: 89
public class MelonfallManager : MonoBehaviour
{
    // Token: 0x17000005 RID: 5
    // (get) Token: 0x060001BA RID: 442 RVA: 0x00008833 File Offset: 0x00006A33
    private float startSpawnTime
    {
        get
        {
            return this.decreaseCurve.Evaluate(this._time);
        }
    }

    // Token: 0x060001BB RID: 443 RVA: 0x00008846 File Offset: 0x00006A46
    private void Start()
    {
        this._animator = base.GetComponent<Animator>();
    }

    // Token: 0x060001BC RID: 444 RVA: 0x00008854 File Offset: 0x00006A54
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.StartGame();
            base.GetComponent<Collider>().enabled = false;
        }
    }

    // Token: 0x060001BD RID: 445 RVA: 0x00008875 File Offset: 0x00006A75
    public void StartGame()
    {
        this._isInGame = true;
        this.timerText.gameObject.SetActive(true);
        MusicManager.instance.EnableMusic(2);
        this._animator.SetTrigger("hasEntered");
    }

    // Token: 0x060001BE RID: 446 RVA: 0x000088AC File Offset: 0x00006AAC
    private void Update()
    {
        if (this._isInGame)
        {
            this.showTime = this.startSpawnTime;
            this._spawnTime -= Time.deltaTime;
            this._time += Time.deltaTime;
            float num = (float)Mathf.FloorToInt((60f - this._time) / 60f);
            float num2 = (float)Mathf.FloorToInt((60f - this._time) % 60f);
            this.timerText.text = string.Format("{0:00}:{1:00}", num, num2);
            if (this._spawnTime <= 0f)
            {
                if (this.startSpawnTime < 1.3f)
                {
                    this.SpawnMelons(3);
                }
                else if (this.startSpawnTime < 2f)
                {
                    this.SpawnMelons(2);
                }
                else
                {
                    this.SpawnMelons(1);
                }
                this._spawnTime = this.startSpawnTime;
            }
            if (this._time >= this.totalGameTime)
            {
                this._isInGame = false;
                this.timerText.gameObject.SetActive(false);
                Melon[] array = Object.FindObjectsOfType<Melon>();
                for (int i = 0; i < array.Length; i++)
                {
                    array[i].DestroyMelon();
                }
                Object.Instantiate<GameObject>(this.melonfallOverSound, base.transform.position, Quaternion.identity);
                this._animator.SetBool("isCompleted", true);
                Action action = this.hasCompletedMelonfall;
                if (action == null)
                {
                    return;
                }
                action();
            }
        }
    }

    // Token: 0x060001BF RID: 447 RVA: 0x00008A14 File Offset: 0x00006C14
    public void SpawnMelons(int numberOfMelons)
    {
        if (numberOfMelons > this.spawnPositions.Length)
        {
            Debug.LogWarning("Number of melons to spawn exceeds available spawn positions.");
            numberOfMelons = this.spawnPositions.Length;
        }
        List<int> list = new List<int>();
        for (int i = 0; i < this.spawnPositions.Length; i++)
        {
            list.Add(i);
        }
        this.ShuffleList(list);
        for (int j = 0; j < numberOfMelons; j++)
        {
            int num = list[j];
            Melon component;
            if (numberOfMelons >= 3 && j == 0)
            {
                component = Object.Instantiate<GameObject>(this.melonShootable, this.spawnPositions[num].position, this.spawnPositions[num].rotation).GetComponent<Melon>();
            }
            else
            {
                component = Object.Instantiate<GameObject>(this.melon, this.spawnPositions[num].position, this.spawnPositions[num].rotation).GetComponent<Melon>();
            }
            component.moveSpeed = 1f / (this.startSpawnTime / 1.3f);
        }
    }

    // Token: 0x060001C0 RID: 448 RVA: 0x00008B00 File Offset: 0x00006D00
    private void ShuffleList(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int index = Random.Range(i, list.Count);
            int value = list[i];
            list[i] = list[index];
            list[index] = value;
        }
    }

    // Token: 0x04000235 RID: 565
    [SerializeField]
    private Transform[] spawnPositions;

    // Token: 0x04000236 RID: 566
    [SerializeField]
    private GameObject melon;

    // Token: 0x04000237 RID: 567
    [SerializeField]
    private GameObject melonShootable;

    // Token: 0x04000238 RID: 568
    [SerializeField]
    private GameObject melonfallOverSound;

    // Token: 0x04000239 RID: 569
    [SerializeField]
    private AnimationCurve decreaseCurve;

    // Token: 0x0400023A RID: 570
    [SerializeField]
    private TextMeshProUGUI timerText;

    // Token: 0x0400023B RID: 571
    private Animator _animator;

    // Token: 0x0400023C RID: 572
    public Action hasCompletedMelonfall;

    // Token: 0x0400023D RID: 573
    public float totalGameTime;

    // Token: 0x0400023E RID: 574
    public float showTime;

    // Token: 0x0400023F RID: 575
    private float _time;

    // Token: 0x04000240 RID: 576
    private float _spawnTime;

    // Token: 0x04000241 RID: 577
    private bool _isInGame;
}

using System;
using UnityEngine;

// Token: 0x0200005A RID: 90
public class MemoryGameTrigger : MonoBehaviour
{
    // Token: 0x060001C2 RID: 450 RVA: 0x00008B51 File Offset: 0x00006D51
    private void Start()
    {
        this.spriteRenderer = base.GetComponent<SpriteRenderer>();
    }

    // Token: 0x060001C3 RID: 451 RVA: 0x00008B5F File Offset: 0x00006D5F
    public void ActivateGame()
    {
        this.spriteRenderer.sprite = this.inGameSprite;
        this.memoryGame.SetActive(true);
    }

    // Token: 0x060001C4 RID: 452 RVA: 0x00008B7E File Offset: 0x00006D7E
    public void CloseGame()
    {
        this.spriteRenderer.sprite = this.interruptGameSprite;
        this.memoryGame.SetActive(false);
    }

    // Token: 0x060001C5 RID: 453 RVA: 0x00008BA0 File Offset: 0x00006DA0
    public void OnGameOver()
    {
        base.GetComponent<Collider>().enabled = false;
        this.memoryGame.SetActive(false);
        this.spriteRenderer.sprite = this.gameOverSprite;
        this.thingsToTeleport.SetActive(true);
        this.player.transform.parent = this.thingsToTeleport.transform;
        this.thingsToTeleport.transform.position = this.teleportDestinationPoint.position;
        this.thingsToTeleport.transform.rotation = this.teleportDestinationPoint.rotation;
        this.player.transform.parent = null;
        this.player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        this.playerCamera.FlipPlayer();
    }

    // Token: 0x04000242 RID: 578
    [SerializeField]
    private GameObject memoryGame;

    // Token: 0x04000243 RID: 579
    [SerializeField]
    private GameObject thingsToTeleport;

    // Token: 0x04000244 RID: 580
    [SerializeField]
    private Transform player;

    // Token: 0x04000245 RID: 581
    [SerializeField]
    private PlayerCamera playerCamera;

    // Token: 0x04000246 RID: 582
    [SerializeField]
    private Transform teleportDestinationPoint;

    // Token: 0x04000247 RID: 583
    [SerializeField]
    private Sprite interruptGameSprite;

    // Token: 0x04000248 RID: 584
    [SerializeField]
    private Sprite inGameSprite;

    // Token: 0x04000249 RID: 585
    [SerializeField]
    private Sprite gameOverSprite;

    // Token: 0x0400024A RID: 586
    private SpriteRenderer spriteRenderer;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Token: 0x02000046 RID: 70
public class Menu : MonoBehaviour
{
    // Token: 0x06000170 RID: 368 RVA: 0x00007D69 File Offset: 0x00005F69
    private void Start()
    {
        this.sceneIndex = PlayerPrefs.GetInt("Scene", 1);
    }

    // Token: 0x06000171 RID: 369 RVA: 0x00007D7C File Offset: 0x00005F7C
    public void LoadGame()
    {
        this.sceneIndex = PlayerPrefs.GetInt("Scene", 1);
        base.StartCoroutine(this.LoadScene_Coroutine(this.sceneIndex));
    }

    // Token: 0x06000172 RID: 370 RVA: 0x00007DA2 File Offset: 0x00005FA2
    public void LoadNewGame()
    {
        PlayerPrefs.DeleteAll();
        this.LoadGame();
    }

    // Token: 0x06000173 RID: 371 RVA: 0x00007DAF File Offset: 0x00005FAF
    public IEnumerator LoadScene_Coroutine(int index)
    {
        this.progressSlider.value = 0f;
        this.LoaderUI.SetActive(true);
        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(index);
        asyncOperation.allowSceneActivation = false;
        float progress = 0f;
        while (!asyncOperation.isDone)
        {
            progress = Mathf.MoveTowards(progress, asyncOperation.progress, Time.deltaTime);
            this.progressSlider.value = progress;
            if (progress >= 0.9f)
            {
                this.progressSlider.value = 1f;
                asyncOperation.allowSceneActivation = true;
            }
            yield return null;
        }
        yield break;
    }

    // Token: 0x040001FF RID: 511
    public int sceneIndex;

    // Token: 0x04000200 RID: 512
    public GameObject LoaderUI;

    // Token: 0x04000201 RID: 513
    public Slider progressSlider;
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Token: 0x02000047 RID: 71
public class MenuExit : MonoBehaviour
{
    // Token: 0x06000175 RID: 373 RVA: 0x00007DD0 File Offset: 0x00005FD0
    public void OnExit()
    {
        Time.timeScale = 1f;
        MusicManager musicManager = Object.FindObjectOfType<MusicManager>();
        if (musicManager.audioSource.clip != musicManager.musicVariants[0])
        {
            musicManager.EnableMusic(0);
        }
        this.SaveItemPresence();
        SceneManager.LoadScene("Menu");
    }

    // Token: 0x06000176 RID: 374 RVA: 0x00007E20 File Offset: 0x00006020
    public void SaveItemPresence()
    {
        Item[] array = this.items;
        for (int i = 0; i < array.Length; i++)
        {
            array[i].SavePresence();
        }
    }

    // Token: 0x04000202 RID: 514
    public Item[] items;
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Token: 0x02000070 RID: 112
public class MoleBotController : MonoBehaviour
{
    // Token: 0x06000219 RID: 537 RVA: 0x0000A5A6 File Offset: 0x000087A6
    private void Awake()
    {
        MoleBotController.Instance = this;
    }

    // Token: 0x0600021A RID: 538 RVA: 0x0000A5AE File Offset: 0x000087AE
    private void Start()
    {
        base.StartCoroutine(this.IdlePunchRoutine());
    }

    // Token: 0x0600021B RID: 539 RVA: 0x0000A5BD File Offset: 0x000087BD
    private IEnumerator IdlePunchRoutine()
    {
        for (; ; )
        {
            yield return new WaitForSeconds(this.idlePunchInterval);
            if (!this.attacking && !this.isPunching && (this.target == null || !this.target.IsUp) && Random.value < this.idlePunchChance)
            {
                this.isPunching = true;
                Vector3 vector = base.transform.position + base.transform.forward * 2f + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
                this.lockedPunchTarget = vector;
                this.animator.SetTrigger("Punch");
            }
        }
        yield break;
    }

    // Token: 0x0600021C RID: 540 RVA: 0x0000A5CC File Offset: 0x000087CC
    private void Update()
    {
        if (this.isPunching)
        {
            Vector3 forward = this.lockedPunchTarget - base.transform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.01f)
            {
                Quaternion b = Quaternion.LookRotation(forward);
                base.transform.rotation = Quaternion.Slerp(base.transform.rotation, b, this.rotateSpeed * Time.deltaTime);
            }
        }
    }

    // Token: 0x0600021D RID: 541 RVA: 0x0000A641 File Offset: 0x00008841
    public void React(MoleController mole)
    {
        this.target = mole;
        if (this.isPunching)
        {
            return;
        }
        if (!this.attacking)
        {
            base.StartCoroutine(this.PunchRoutine());
        }
    }

    // Token: 0x0600021E RID: 542 RVA: 0x0000A668 File Offset: 0x00008868
    public IEnumerator DelayedRePunch()
    {
        yield return new WaitForSeconds(this.repunchDelay);
        if (this.target != null && this.target.IsUp && !this.isPunching)
        {
            base.StartCoroutine(this.PunchRoutine());
        }
        yield break;
    }

    // Token: 0x0600021F RID: 543 RVA: 0x0000A677 File Offset: 0x00008877
    public void MoleWentDown()
    {
        if (this.isPunching)
        {
            return;
        }
        this.attacking = false;
        this.lockedPunchTarget = Vector3.zero;
        this.target = null;
    }

    // Token: 0x06000220 RID: 544 RVA: 0x0000A69B File Offset: 0x0000889B
    private IEnumerator PunchRoutine()
    {
        this.attacking = true;
        if (this.target == null)
        {
            yield break;
        }
        yield return new WaitForSeconds(this.reactionDelay);
        if (this.target == null)
        {
            yield break;
        }
        if (this.target.IsUp || (!this.target.IsUp && Random.value < this.randomPunchChanceDown))
        {
            this.isPunching = true;
            if (Random.value < this.mistakeChance && this.target.currentUp != null)
            {
                this.lockedPunchTarget = this.target.currentUp.position + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
            }
            else if (this.target.currentUp != null)
            {
                this.lockedPunchTarget = this.target.currentUp.position;
            }
            this.animator.SetTrigger("Punch");
        }
        else
        {
            this.isPunching = false;
            this.attacking = false;
        }
        yield break;
    }

    // Token: 0x06000221 RID: 545 RVA: 0x0000A6AC File Offset: 0x000088AC
    public void CheckHit()
    {
        bool flag = false;
        if (this.target != null && this.target.IsUp && !this.target.hasBeenHit && this.target.currentUp != null && Vector3.Distance(this.target.currentUp.position, this.lockedPunchTarget) < 0.5f)
        {
            this.target.GotHit();
            flag = true;
            Debug.Log("Bot hit the mole!");
        }
        this.isPunching = false;
        this.lockedPunchTarget = Vector3.zero;
        if (!flag && this.target != null && this.target.IsUp)
        {
            base.StartCoroutine(this.DelayedRePunch());
            return;
        }
        float delay = Random.Range(0f, this.postHitDelayMax);
        base.StartCoroutine(this.PostHitDelay(delay));
    }

    // Token: 0x06000222 RID: 546 RVA: 0x0000A78C File Offset: 0x0000898C
    private IEnumerator PostHitDelay(float delay)
    {
        this.attacking = false;
        yield return new WaitForSeconds(delay);
        yield break;
    }

    // Token: 0x06000223 RID: 547 RVA: 0x0000A7A4 File Offset: 0x000089A4
    public void ResetMoles()
    {
        this.attacking = false;
        this.isPunching = false;
        this.lockedPunchTarget = Vector3.zero;
        this.target = null;
        foreach (MoleController moleController in this.allMoles)
        {
            moleController.GoDown();
            moleController.hasBeenHit = false;
            moleController.IsUp = false;
            moleController.hasScoredThisUp = false;
            moleController.stayTimer = 0f;
        }
    }

    // Token: 0x040002A6 RID: 678
    public static MoleBotController Instance;

    // Token: 0x040002A7 RID: 679
    public List<MoleController> allMoles = new List<MoleController>();

    // Token: 0x040002A8 RID: 680
    public Animator animator;

    // Token: 0x040002A9 RID: 681
    [Header("Bot Settings")]
    public float reactionDelay = 0.8f;

    // Token: 0x040002AA RID: 682
    public float randomPunchChanceDown = 0.3f;

    // Token: 0x040002AB RID: 683
    public float rotateSpeed = 6f;

    // Token: 0x040002AC RID: 684
    public float mistakeChance = 0.4f;

    // Token: 0x040002AD RID: 685
    public float postHitDelayMax = 0.5f;

    // Token: 0x040002AE RID: 686
    [HideInInspector]
    public MoleController target;

    // Token: 0x040002AF RID: 687
    [Header("Idle Punch")]
    public float idlePunchInterval = 2.5f;

    // Token: 0x040002B0 RID: 688
    public float idlePunchChance = 0.4f;

    // Token: 0x040002B1 RID: 689
    [Header("Timing")]
    public float repunchDelay = 0.4f;

    // Token: 0x040002B2 RID: 690
    private Vector3 lockedPunchTarget;

    // Token: 0x040002B3 RID: 691
    private bool attacking;

    // Token: 0x040002B4 RID: 692
    private bool isPunching;
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Token: 0x02000071 RID: 113
public class MoleController : MonoBehaviour
{
    // Token: 0x06000225 RID: 549 RVA: 0x0000A8AC File Offset: 0x00008AAC
    private void Update()
    {
        this.HandleInput();
        this.MoveVertically();
        if (this.IsUp && !this.hasScoredThisUp)
        {
            this.stayTimer += Time.deltaTime;
            if (this.stayTimer >= this.stayToScoreTime)
            {
                this.EarnPoint();
                return;
            }
        }
        else
        {
            this.stayTimer = 0f;
        }
    }

    // Token: 0x06000226 RID: 550 RVA: 0x0000A908 File Offset: 0x00008B08
    private void HandleInput()
    {
        if (!this.IsUp)
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                this.SetHole(this.holeW, this.downW);
            }
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                this.SetHole(this.holeA, this.downA);
            }
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                this.SetHole(this.holeS, this.downS);
            }
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                this.SetHole(this.holeD, this.downD);
            }
        }
        if (this.IsUp && this.currentUp != null && ((this.currentUp == this.holeW && !Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.UpArrow)) || (this.currentUp == this.holeA && !Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.LeftArrow)) || (this.currentUp == this.holeS && !Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.DownArrow)) || (this.currentUp == this.holeD && !Input.GetKey(KeyCode.D) && !Input.GetKey(KeyCode.RightArrow))))
        {
            this.GoDown();
        }
    }

    // Token: 0x06000227 RID: 551 RVA: 0x0000AA90 File Offset: 0x00008C90
    private void SetHole(Transform up, Transform down)
    {
        this.currentUp = up;
        this.currentDown = down;
        Vector3 position = base.transform.position;
        position.x = up.position.x;
        position.z = up.position.z;
        base.transform.position = position;
        this.IsUp = true;
        this.hasBeenHit = false;
        this.hasScoredThisUp = false;
        this.stayTimer = 0f;
        this.animator.SetBool("Up", true);
        MoleBotController.Instance.React(this);
    }

    // Token: 0x06000228 RID: 552 RVA: 0x0000AB23 File Offset: 0x00008D23
    public void GoDown()
    {
        if (!this.IsUp)
        {
            return;
        }
        this.IsUp = false;
        this.hasScoredThisUp = false;
        this.animator.SetBool("Up", false);
        MoleBotController.Instance.MoleWentDown();
    }

    // Token: 0x06000229 RID: 553 RVA: 0x0000AB58 File Offset: 0x00008D58
    private void MoveVertically()
    {
        if (this.currentUp == null || this.currentDown == null)
        {
            return;
        }
        Transform transform = this.IsUp ? this.currentUp : this.currentDown;
        Vector3 position = base.transform.position;
        position.y = Mathf.MoveTowards(position.y, transform.position.y, this.moveSpeed * Time.deltaTime);
        base.transform.position = position;
    }

    // Token: 0x0600022A RID: 554 RVA: 0x0000ABDC File Offset: 0x00008DDC
    public void GotHit()
    {
        if (!this.IsUp || this.hasBeenHit)
        {
            return;
        }
        this.hasBeenHit = true;
        this.botScore++;
        this.UpdateUI();
        base.StartCoroutine(this.ActivatePlus(false));
        this.CheckWin();
        this.GoDown();
    }

    // Token: 0x0600022B RID: 555 RVA: 0x0000AC30 File Offset: 0x00008E30
    public void EarnPoint()
    {
        if (!this.IsUp || this.hasScoredThisUp)
        {
            return;
        }
        this.hasScoredThisUp = true;
        this.playerScore++;
        this.UpdateUI();
        base.StartCoroutine(this.ActivatePlus(true));
        this.CheckWin();
        base.StartCoroutine(MoleBotController.Instance.DelayedRePunch());
    }

    // Token: 0x0600022C RID: 556 RVA: 0x0000AC8E File Offset: 0x00008E8E
    public IEnumerator ActivatePlus(bool state)
    {
        GameObject original = state ? this.PlusOne : this.MinusOne;
        if (this.currentUp != null)
        {
            Object.Instantiate<GameObject>(original, this.currentUp.transform.position, Quaternion.identity);
        }
        yield return new WaitForSeconds(1f);
        yield break;
    }

    // Token: 0x0600022D RID: 557 RVA: 0x0000ACA4 File Offset: 0x00008EA4
    private void UpdateUI()
    {
        this.scoreText.text = string.Format("{0} : {1}", this.playerScore, this.botScore);
    }

    // Token: 0x0600022E RID: 558 RVA: 0x0000ACD1 File Offset: 0x00008ED1
    public IEnumerator ReturnToGame()
    {
        yield return new WaitForSeconds(1.5f);
        this.gameManager.DisableGame();
        yield break;
    }

    // Token: 0x0600022F RID: 559 RVA: 0x0000ACE0 File Offset: 0x00008EE0
    private void CheckWin()
    {
        if (this.playerScore >= this.winScore)
        {
            this.resultText.text = "победа!";
            base.StartCoroutine(this.ReturnToGame());
            return;
        }
        if (this.botScore >= this.winScore)
        {
            this.resultText.text = "поражение :(";
            this.ShowRetryUI();
        }
    }

    // Token: 0x06000230 RID: 560 RVA: 0x0000AD3D File Offset: 0x00008F3D
    private void ShowRetryUI()
    {
        if (this.RetryPanel != null)
        {
            this.RetryPanel.SetActive(true);
        }
        CursorController.UnlockCursor();
    }

    // Token: 0x06000231 RID: 561 RVA: 0x0000AD60 File Offset: 0x00008F60
    public void RetryGame()
    {
        this.playerScore = 0;
        this.botScore = 0;
        this.scoreText.text = string.Format("{0} : {1}", this.playerScore, this.botScore);
        this.resultText.text = "";
        if (this.RetryPanel != null)
        {
            this.RetryPanel.SetActive(false);
        }
        MoleBotController.Instance.ResetMoles();
        CursorController.LockCursor();
    }

    // Token: 0x040002B5 RID: 693
    [Header("Holes")]
    public Transform holeW;

    // Token: 0x040002B6 RID: 694
    [Header("Holes")]
    public Transform holeA;

    // Token: 0x040002B7 RID: 695
    [Header("Holes")]
    public Transform holeS;

    // Token: 0x040002B8 RID: 696
    [Header("Holes")]
    public Transform holeD;

    // Token: 0x040002B9 RID: 697
    public Transform downW;

    // Token: 0x040002BA RID: 698
    public Transform downA;

    // Token: 0x040002BB RID: 699
    public Transform downS;

    // Token: 0x040002BC RID: 700
    public Transform downD;

    // Token: 0x040002BD RID: 701
    [Header("Settings")]
    public float moveSpeed = 6f;

    // Token: 0x040002BE RID: 702
    public float stayToScoreTime = 1.5f;

    // Token: 0x040002BF RID: 703
    public int winScore = 20;

    // Token: 0x040002C0 RID: 704
    [Header("Scores")]
    public int playerScore;

    // Token: 0x040002C1 RID: 705
    public int botScore;

    // Token: 0x040002C2 RID: 706
    [Header("References")]
    public GameObject RetryPanel;

    // Token: 0x040002C3 RID: 707
    public Button retryButton;

    // Token: 0x040002C4 RID: 708
    public Animator animator;

    // Token: 0x040002C5 RID: 709
    public TextMeshProUGUI scoreText;

    // Token: 0x040002C6 RID: 710
    public TextMeshProUGUI resultText;

    // Token: 0x040002C7 RID: 711
    [HideInInspector]
    public bool IsUp;

    // Token: 0x040002C8 RID: 712
    [HideInInspector]
    public bool hasBeenHit;

    // Token: 0x040002C9 RID: 713
    [HideInInspector]
    public Transform currentUp;

    // Token: 0x040002CA RID: 714
    [HideInInspector]
    public Transform currentDown;

    // Token: 0x040002CB RID: 715
    [HideInInspector]
    public bool hasScoredThisUp;

    // Token: 0x040002CC RID: 716
    public SahurGameManager gameManager;

    // Token: 0x040002CD RID: 717
    public GameObject PlusOne;

    // Token: 0x040002CE RID: 718
    public GameObject MinusOne;

    // Token: 0x040002CF RID: 719
    public float stayTimer;
}

using System;
using UnityEngine;

// Token: 0x0200005B RID: 91
public class MoonGravityTrigger : MonoBehaviour
{
    // Token: 0x060001C7 RID: 455 RVA: 0x00008C7B File Offset: 0x00006E7B
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !this._hasTriggered)
        {
            this._hasTriggered = true;
            if (this.isMoon)
            {
                GravityController.instance.SetMoonGravity();
                return;
            }
            GravityController.instance.SetEarthGravity();
        }
    }

    // Token: 0x0400024B RID: 587
    public bool isMoon;

    // Token: 0x0400024C RID: 588
    private bool _hasTriggered;
}

using System;
using UnityEngine;

// Token: 0x0200008B RID: 139
public class MouseLook : MonoBehaviour
{
    // Token: 0x060002B4 RID: 692 RVA: 0x0000D0BC File Offset: 0x0000B2BC
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Token: 0x060002B5 RID: 693 RVA: 0x0000D0C4 File Offset: 0x0000B2C4
    private void Update()
    {
        float d = Input.GetAxis("Mouse X") * this.mouseSensitivity * Time.deltaTime;
        float num = Input.GetAxis("Mouse Y") * this.mouseSensitivity * Time.deltaTime;
        this.xRotation -= num;
        this.xRotation = Mathf.Clamp(this.xRotation, -90f, 90f);
        base.transform.localRotation = Quaternion.Euler(this.xRotation, 0f, 0f);
        this.playerBody.Rotate(Vector3.up * d);
    }

    // Token: 0x04000390 RID: 912
    public float mouseSensitivity = 100f;

    // Token: 0x04000391 RID: 913
    public Transform playerBody;

    // Token: 0x04000392 RID: 914
    private float xRotation;
}

using System;
using UnityEngine;

// Token: 0x02000038 RID: 56
public class MoveCamera : MonoBehaviour
{
    // Token: 0x0600012F RID: 303 RVA: 0x00006EB4 File Offset: 0x000050B4
    private void FixedUpdate()
    {
        if (this.currentMode == MoveCamera.UpdateMode.FixedUpdate)
        {
            this.ApplyMovement();
        }
    }

    // Token: 0x06000130 RID: 304 RVA: 0x00006EC5 File Offset: 0x000050C5
    private void Update()
    {
        if (this.currentMode == MoveCamera.UpdateMode.Update)
        {
            this.ApplyMovement();
        }
    }

    // Token: 0x06000131 RID: 305 RVA: 0x00006ED6 File Offset: 0x000050D6
    private void LateUpdate()
    {
        if (this.currentMode == MoveCamera.UpdateMode.LateUpdate)
        {
            this.ApplyMovement();
        }
    }

    // Token: 0x06000132 RID: 306 RVA: 0x00006EE6 File Offset: 0x000050E6
    private void ApplyMovement()
    {
        base.transform.position = this.cameraPosition.position;
    }

    // Token: 0x040001A8 RID: 424
    public Transform cameraPosition;

    // Token: 0x040001A9 RID: 425
    public MoveCamera.UpdateMode currentMode;

    // Token: 0x020000E3 RID: 227
    public enum UpdateMode
    {
        // Token: 0x04000556 RID: 1366
        LateUpdate,
        // Token: 0x04000557 RID: 1367
        Update,
        // Token: 0x04000558 RID: 1368
        FixedUpdate
    }
}

using System;
using UnityEngine;

// Token: 0x02000039 RID: 57
public class MoveForward : MonoBehaviour
{
    // Token: 0x06000134 RID: 308 RVA: 0x00006F06 File Offset: 0x00005106
    private void Update()
    {
        base.transform.Translate(Vector3.forward * this.speed * Time.deltaTime);
    }

    // Token: 0x040001AA RID: 426
    public float speed;
}

using System;
using UnityEngine;

// Token: 0x02000072 RID: 114
public class MovingPlatform : MonoBehaviour
{
    // Token: 0x06000233 RID: 563 RVA: 0x0000AE08 File Offset: 0x00009008
    private void Start()
    {
        if (this.pointA == null || this.pointB == null)
        {
            Debug.LogError("MovingPlatform: Assign both Point A and Point B.");
            return;
        }
        base.transform.position = this.pointA.position;
        this.targetPosition = this.pointB.position;
    }

    // Token: 0x06000234 RID: 564 RVA: 0x0000AE64 File Offset: 0x00009064
    private void FixedUpdate()
    {
        Vector3 position = Vector3.MoveTowards(base.transform.position, this.targetPosition, this.speed * Time.fixedDeltaTime);
        base.transform.position = position;
        if (Vector3.Distance(base.transform.position, this.targetPosition) < 0.01f)
        {
            this.targetPosition = ((this.targetPosition == this.pointA.position) ? this.pointB.position : this.pointA.position);
        }
    }

    // Token: 0x06000235 RID: 565 RVA: 0x0000AEF3 File Offset: 0x000090F3
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            collision.transform.SetParent(base.transform);
        }
    }

    // Token: 0x06000236 RID: 566 RVA: 0x0000AF18 File Offset: 0x00009118
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            collision.transform.SetParent(null);
        }
    }

    // Token: 0x040002D0 RID: 720
    [Header("Movement Points")]
    public Transform pointA;

    // Token: 0x040002D1 RID: 721
    public Transform pointB;

    // Token: 0x040002D2 RID: 722
    [Header("Movement Settings")]
    public float speed = 2f;

    // Token: 0x040002D3 RID: 723
    private Vector3 targetPosition;
}

using System;
using UnityEngine;

// Token: 0x0200001C RID: 28
public class MushroomChecker : MonoBehaviour
{
    // Token: 0x060000A0 RID: 160 RVA: 0x000047A1 File Offset: 0x000029A1
    private void Start()
    {
        this.collector = Object.FindObjectOfType<ItemCollector>();
    }

    // Token: 0x060000A1 RID: 161 RVA: 0x000047B0 File Offset: 0x000029B0
    private void Update()
    {
        this.playerNearGate = Physics.CheckSphere(this.openingPoint.position, this.sightRange, this.whatIsPlayer);
        this.playerPastGate = Physics.CheckSphere(this.closingPoint.position, this.sightRange, this.whatIsPlayer);
        if (this.playerNearGate)
        {
            this.CheckForMushrooms();
        }
        if (this.playerPastGate)
        {
            this.CloseGate();
        }
    }

    // Token: 0x060000A2 RID: 162 RVA: 0x00004828 File Offset: 0x00002A28
    public void CheckForMushrooms()
    {
        if (this.collector.items >= this.mushroomsRequired)
        {
            if (this.sable != null && !this.sable.activeInHierarchy)
            {
                return;
            }
            this.gateAnimator.SetBool("hasMushrooms", true);
        }
        if (!this.alreadySaved)
        {
            this.alreadySaved = true;
            this.collector.SaveMushrooms();
            this.collector.GetComponent<PlayerController>().SavePosition();
            Object.FindObjectOfType<MenuExit>(true).SaveItemPresence();
        }
    }

    // Token: 0x060000A3 RID: 163 RVA: 0x000048AA File Offset: 0x00002AAA
    public void CloseGate()
    {
        this.gateAnimator.SetBool("pastGate", true);
        base.enabled = false;
    }

    // Token: 0x060000A4 RID: 164 RVA: 0x000048C4 File Offset: 0x00002AC4
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(this.openingPoint.position, this.sightRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(this.closingPoint.position, this.sightRange);
    }

    // Token: 0x040000D2 RID: 210
    public GameObject sable;

    // Token: 0x040000D3 RID: 211
    public Transform openingPoint;

    // Token: 0x040000D4 RID: 212
    public Transform closingPoint;

    // Token: 0x040000D5 RID: 213
    public Animator gateAnimator;

    // Token: 0x040000D6 RID: 214
    public LayerMask whatIsPlayer;

    // Token: 0x040000D7 RID: 215
    public float sightRange;

    // Token: 0x040000D8 RID: 216
    public int mushroomsRequired;

    // Token: 0x040000D9 RID: 217
    private ItemCollector collector;

    // Token: 0x040000DA RID: 218
    private bool playerNearGate;

    // Token: 0x040000DB RID: 219
    private bool playerPastGate;

    // Token: 0x040000DC RID: 220
    private bool alreadySaved;
}

using System;
using UnityEngine;

// Token: 0x02000026 RID: 38
public class MushroomDamage : MonoBehaviour
{
    // Token: 0x060000DD RID: 221 RVA: 0x000056C8 File Offset: 0x000038C8
    private void Start()
    {
        this.shailushay = Object.FindObjectOfType<Shailushay>();
    }

    // Token: 0x060000DE RID: 222 RVA: 0x000056D5 File Offset: 0x000038D5
    private void OnDestroy()
    {
        if (this.shailushay != null)
        {
            this.shailushay.TakeDamage(this.damage);
        }
    }

    // Token: 0x04000136 RID: 310
    [SerializeField]
    private int damage;

    // Token: 0x04000137 RID: 311
    private Shailushay shailushay;
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Token: 0x0200001D RID: 29
public class MushroomGame : MonoBehaviour
{
    // Token: 0x060000A6 RID: 166 RVA: 0x00004919 File Offset: 0x00002B19
    private void OnTriggerEnter(Collider other)
    {
        if (!this.isReady && other.transform.root.CompareTag("Player"))
        {
            this.StartGame();
        }
    }

    // Token: 0x060000A7 RID: 167 RVA: 0x00004940 File Offset: 0x00002B40
    private void StartGame()
    {
        this.gameTime = this.startGameTime;
        this.timerText.gameObject.SetActive(true);
        this.isReady = true;
        base.GetComponent<SphereCollider>().enabled = false;
        Object.FindObjectOfType<MusicManager>().EnableMusic(1);
    }

    // Token: 0x060000A8 RID: 168 RVA: 0x00004980 File Offset: 0x00002B80
    private void Update()
    {
        if (this.isReady)
        {
            if (this.gameTime > 0f)
            {
                float num = (float)Mathf.FloorToInt(this.gameTime / 60f);
                float num2 = (float)Mathf.FloorToInt(this.gameTime % 60f);
                this.timerText.text = string.Format("{0:00}:{1:00}", num, num2);
                this.gameTime -= Time.deltaTime;
                if (this.timeBetweenSpawn > 0f)
                {
                    this.timeBetweenSpawn -= Time.deltaTime;
                    return;
                }
                int num3 = Random.Range(0, this.enemies.Length);
                int num4 = Random.Range(0, this.spawnPoints.Length);
                Object.Instantiate<GameObject>(this.enemies[num3], this.spawnPoints[num4].position, Quaternion.identity);
                this.timeBetweenSpawn = this.startTimeBetweenSpawn;
                if (this.startTimeBetweenSpawn > this.minTime)
                {
                    this.startTimeBetweenSpawn -= this.decreaseTime;
                    return;
                }
            }
            else if (!this.alreadyStarted)
            {
                this.alreadyStarted = true;
                base.StartCoroutine(this.StartBossfight());
            }
        }
    }

    // Token: 0x060000A9 RID: 169 RVA: 0x00004AA9 File Offset: 0x00002CA9
    public IEnumerator StartBossfight()
    {
        MushroomHunter[] array = Object.FindObjectsOfType<MushroomHunter>();
        for (int i = 0; i < array.Length; i++)
        {
            array[i].TakeDamage(1000);
        }
        this.timerText.gameObject.SetActive(false);
        this.mushroomAnimator.SetBool("gameOver", true);
        yield return new WaitForSeconds(2f);
        this.player.transform.position = this.posBeforeBossfight.position;
        this.player.transform.rotation = this.posBeforeBossfight.rotation;
        this.player.enabled = false;
        yield return new WaitForSeconds(2f);
        this.fenceAnimator.SetBool("bossfightStarted", true);
        yield return new WaitForSeconds(4f);
        this.bossHP.SetActive(true);
        Object.Instantiate<GameObject>(this.boss, this.bossSpawnPos.position, Quaternion.identity);
        this.player.enabled = true;
        base.enabled = false;
        yield break;
    }

    // Token: 0x040000DD RID: 221
    public GameObject[] enemies;

    // Token: 0x040000DE RID: 222
    public Transform[] spawnPoints;

    // Token: 0x040000DF RID: 223
    public TextMeshProUGUI timerText;

    // Token: 0x040000E0 RID: 224
    public PlayerController player;

    // Token: 0x040000E1 RID: 225
    public Animator mushroomAnimator;

    // Token: 0x040000E2 RID: 226
    public Animator fenceAnimator;

    // Token: 0x040000E3 RID: 227
    public GameObject boss;

    // Token: 0x040000E4 RID: 228
    public GameObject bossHP;

    // Token: 0x040000E5 RID: 229
    public Transform posBeforeBossfight;

    // Token: 0x040000E6 RID: 230
    public Transform bossSpawnPos;

    // Token: 0x040000E7 RID: 231
    private float gameTime;

    // Token: 0x040000E8 RID: 232
    public float startGameTime;

    // Token: 0x040000E9 RID: 233
    private float timeBetweenSpawn;

    // Token: 0x040000EA RID: 234
    public float startTimeBetweenSpawn;

    // Token: 0x040000EB RID: 235
    public float decreaseTime;

    // Token: 0x040000EC RID: 236
    public float minTime;

    // Token: 0x040000ED RID: 237
    private bool isReady;

    // Token: 0x040000EE RID: 238
    private bool alreadyStarted;
}

using System;
using UnityEngine;
using UnityEngine.AI;

// Token: 0x0200001E RID: 30
public class MushroomHunter : MonoBehaviour
{
    // Token: 0x060000AB RID: 171 RVA: 0x00004AC0 File Offset: 0x00002CC0
    private void Awake()
    {
        this.agent = base.GetComponent<NavMeshAgent>();
    }

    // Token: 0x060000AC RID: 172 RVA: 0x00004ACE File Offset: 0x00002CCE
    private void Start()
    {
        this.walkPoint = base.transform.position;
    }

    // Token: 0x060000AD RID: 173 RVA: 0x00004AE4 File Offset: 0x00002CE4
    private void Update()
    {
        this.FindClosestMushroom();
        this.mushroomInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsMushroom);
        this.mushroomInPickRange = Physics.CheckSphere(base.transform.position, this.pickRange, this.whatIsMushroom);
        if (!this.hasPicked)
        {
            if (!this.mushroomInSightRange && !this.mushroomInPickRange)
            {
                this.Patroling();
            }
            if (this.mushroomInSightRange && !this.mushroomInPickRange)
            {
                this.ChaseMushroom();
            }
            if (this.mushroomInPickRange && this.mushroomInSightRange)
            {
                this.PickMushroom();
                return;
            }
        }
        else
        {
            this.Patroling();
        }
    }

    // Token: 0x060000AE RID: 174 RVA: 0x00004B98 File Offset: 0x00002D98
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 5f)
        {
            this.walkPointSet = false;
            if (this.hasPicked)
            {
                this.hasPicked = false;
                this.DestroyEnemy();
            }
        }
    }

    // Token: 0x060000AF RID: 175 RVA: 0x00004C10 File Offset: 0x00002E10
    public void FindClosestMushroom()
    {
        float num = float.PositiveInfinity;
        foreach (GameObject gameObject in GameObject.FindGameObjectsWithTag("CentralMushroom"))
        {
            float sqrMagnitude = (gameObject.transform.position - base.transform.position).sqrMagnitude;
            if (sqrMagnitude < num)
            {
                num = sqrMagnitude;
                this.closestMushroom = gameObject.transform;
            }
        }
    }

    // Token: 0x060000B0 RID: 176 RVA: 0x00004C7A File Offset: 0x00002E7A
    private void SearchWalkPoint()
    {
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x060000B1 RID: 177 RVA: 0x00004CB0 File Offset: 0x00002EB0
    private void ChaseMushroom()
    {
        this.agent.SetDestination(this.closestMushroom.position);
    }

    // Token: 0x060000B2 RID: 178 RVA: 0x00004CCC File Offset: 0x00002ECC
    private void PickMushroom()
    {
        this.hasPicked = true;
        this.closestMushroom.transform.parent = this.mushroomPos;
        this.closestMushroom.transform.position = this.mushroomPos.position;
        this.closestMushroom.transform.rotation = this.mushroomPos.rotation;
    }

    // Token: 0x060000B3 RID: 179 RVA: 0x00004D2C File Offset: 0x00002F2C
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            this.DestroyEnemy();
        }
    }

    // Token: 0x060000B4 RID: 180 RVA: 0x00004D50 File Offset: 0x00002F50
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        if (this.hasPicked)
        {
            Object.Instantiate<GameObject>(this.newMushroom, base.transform.position, Quaternion.identity);
        }
        Object.Destroy(base.gameObject);
    }

    // Token: 0x060000B5 RID: 181 RVA: 0x00004DA8 File Offset: 0x00002FA8
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.pickRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x040000EF RID: 239
    public NavMeshAgent agent;

    // Token: 0x040000F0 RID: 240
    public Transform mushroomPos;

    // Token: 0x040000F1 RID: 241
    public Transform closestMushroom;

    // Token: 0x040000F2 RID: 242
    public LayerMask whatIsGround;

    // Token: 0x040000F3 RID: 243
    public LayerMask whatIsMushroom;

    // Token: 0x040000F4 RID: 244
    public Vector3 walkPoint;

    // Token: 0x040000F5 RID: 245
    private bool walkPointSet;

    // Token: 0x040000F6 RID: 246
    public float walkPointRange;

    // Token: 0x040000F7 RID: 247
    public float sightRange;

    // Token: 0x040000F8 RID: 248
    public float pickRange;

    // Token: 0x040000F9 RID: 249
    public bool mushroomInSightRange;

    // Token: 0x040000FA RID: 250
    public bool mushroomInPickRange;

    // Token: 0x040000FB RID: 251
    public bool hasPicked;

    // Token: 0x040000FC RID: 252
    public float health;

    // Token: 0x040000FD RID: 253
    public int damage;

    // Token: 0x040000FE RID: 254
    public GameObject deathEffect;

    // Token: 0x040000FF RID: 255
    public GameObject newMushroom;
}

using System;
using UnityEngine;

// Token: 0x02000041 RID: 65
public class MusicManager : MonoBehaviour
{
    // Token: 0x06000158 RID: 344 RVA: 0x000078F1 File Offset: 0x00005AF1
    private void Awake()
    {
        if (MusicManager.instance != null)
        {
            Object.Destroy(base.gameObject);
            return;
        }
        MusicManager.instance = this;
        Object.DontDestroyOnLoad(base.transform.gameObject);
    }

    // Token: 0x06000159 RID: 345 RVA: 0x00007922 File Offset: 0x00005B22
    private void Start()
    {
        this.audioSource.clip = this.musicVariants[0];
        this.audioSource.Play();
    }

    // Token: 0x0600015A RID: 346 RVA: 0x00007944 File Offset: 0x00005B44
    public void EnableMusic(int musicIndex)
    {
        if (this.audioSource.clip != this.musicVariants[musicIndex])
        {
            this.audioSource.clip = this.musicVariants[musicIndex];
            this.audioSource.volume = this.volumes[musicIndex];
            this.audioSource.Play();
        }
    }

    // Token: 0x040001E4 RID: 484
    public static MusicManager instance;

    // Token: 0x040001E5 RID: 485
    public AudioSource audioSource;

    // Token: 0x040001E6 RID: 486
    public AudioClip[] musicVariants;

    // Token: 0x040001E7 RID: 487
    [Range(0f, 1f)]
    public float[] volumes;
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Token: 0x0200008D RID: 141
[DisallowMultipleComponent]
public class Outline : MonoBehaviour
{
    // Token: 0x17000012 RID: 18
    // (get) Token: 0x060002B9 RID: 697 RVA: 0x0000D300 File Offset: 0x0000B500
    // (set) Token: 0x060002BA RID: 698 RVA: 0x0000D308 File Offset: 0x0000B508
    public Outline.Mode OutlineMode
    {
        get
        {
            return this.outlineMode;
        }
        set
        {
            this.outlineMode = value;
            this.needsUpdate = true;
        }
    }

    // Token: 0x17000013 RID: 19
    // (get) Token: 0x060002BB RID: 699 RVA: 0x0000D318 File Offset: 0x0000B518
    // (set) Token: 0x060002BC RID: 700 RVA: 0x0000D320 File Offset: 0x0000B520
    public Color OutlineColor
    {
        get
        {
            return this.outlineColor;
        }
        set
        {
            this.outlineColor = value;
            this.needsUpdate = true;
        }
    }

    // Token: 0x17000014 RID: 20
    // (get) Token: 0x060002BD RID: 701 RVA: 0x0000D330 File Offset: 0x0000B530
    // (set) Token: 0x060002BE RID: 702 RVA: 0x0000D338 File Offset: 0x0000B538
    public float OutlineWidth
    {
        get
        {
            return this.outlineWidth;
        }
        set
        {
            this.outlineWidth = value;
            this.needsUpdate = true;
        }
    }

    // Token: 0x060002BF RID: 703 RVA: 0x0000D348 File Offset: 0x0000B548
    private void Awake()
    {
        this.renderers = base.GetComponentsInChildren<Renderer>();
        this.outlineMaskMaterial = Object.Instantiate<Material>(Resources.Load<Material>("Materials/OutlineMask"));
        this.outlineFillMaterial = Object.Instantiate<Material>(Resources.Load<Material>("Materials/OutlineFill"));
        this.outlineMaskMaterial.name = "OutlineMask (Instance)";
        this.outlineFillMaterial.name = "OutlineFill (Instance)";
        this.LoadSmoothNormals();
        this.needsUpdate = true;
    }

    // Token: 0x060002C0 RID: 704 RVA: 0x0000D3B8 File Offset: 0x0000B5B8
    private void OnEnable()
    {
        foreach (Renderer renderer in this.renderers)
        {
            List<Material> list = renderer.sharedMaterials.ToList<Material>();
            list.Add(this.outlineMaskMaterial);
            list.Add(this.outlineFillMaterial);
            renderer.materials = list.ToArray();
        }
    }

    // Token: 0x060002C1 RID: 705 RVA: 0x0000D40C File Offset: 0x0000B60C
    private void OnMouseOver()
    {
        this.outlineWidth = 10f;
        this.needsUpdate = true;
    }

    // Token: 0x060002C2 RID: 706 RVA: 0x0000D420 File Offset: 0x0000B620
    private void OnMouseExit()
    {
        this.outlineWidth = 0f;
        this.needsUpdate = true;
    }

    // Token: 0x060002C3 RID: 707 RVA: 0x0000D434 File Offset: 0x0000B634
    private void OnValidate()
    {
        this.needsUpdate = true;
        if ((!this.precomputeOutline && this.bakeKeys.Count != 0) || this.bakeKeys.Count != this.bakeValues.Count)
        {
            this.bakeKeys.Clear();
            this.bakeValues.Clear();
        }
        if (this.precomputeOutline && this.bakeKeys.Count == 0)
        {
            this.Bake();
        }
    }

    // Token: 0x060002C4 RID: 708 RVA: 0x0000D4A6 File Offset: 0x0000B6A6
    private void Update()
    {
        if (this.needsUpdate)
        {
            this.needsUpdate = false;
            this.UpdateMaterialProperties();
        }
    }

    // Token: 0x060002C5 RID: 709 RVA: 0x0000D4C0 File Offset: 0x0000B6C0
    private void OnDisable()
    {
        foreach (Renderer renderer in this.renderers)
        {
            List<Material> list = renderer.sharedMaterials.ToList<Material>();
            list.Remove(this.outlineMaskMaterial);
            list.Remove(this.outlineFillMaterial);
            renderer.materials = list.ToArray();
        }
    }

    // Token: 0x060002C6 RID: 710 RVA: 0x0000D516 File Offset: 0x0000B716
    private void OnDestroy()
    {
        Object.Destroy(this.outlineMaskMaterial);
        Object.Destroy(this.outlineFillMaterial);
    }

    // Token: 0x060002C7 RID: 711 RVA: 0x0000D530 File Offset: 0x0000B730
    private void Bake()
    {
        HashSet<Mesh> hashSet = new HashSet<Mesh>();
        foreach (MeshFilter meshFilter in base.GetComponentsInChildren<MeshFilter>())
        {
            if (hashSet.Add(meshFilter.sharedMesh))
            {
                List<Vector3> data = this.SmoothNormals(meshFilter.sharedMesh);
                this.bakeKeys.Add(meshFilter.sharedMesh);
                this.bakeValues.Add(new Outline.ListVector3
                {
                    data = data
                });
            }
        }
    }

    // Token: 0x060002C8 RID: 712 RVA: 0x0000D5A4 File Offset: 0x0000B7A4
    private void LoadSmoothNormals()
    {
        foreach (MeshFilter meshFilter in base.GetComponentsInChildren<MeshFilter>())
        {
            if (Outline.registeredMeshes.Add(meshFilter.sharedMesh))
            {
                int num = this.bakeKeys.IndexOf(meshFilter.sharedMesh);
                List<Vector3> uvs = (num >= 0) ? this.bakeValues[num].data : this.SmoothNormals(meshFilter.sharedMesh);
                meshFilter.sharedMesh.SetUVs(3, uvs);
                Renderer component = meshFilter.GetComponent<Renderer>();
                if (component != null)
                {
                    this.CombineSubmeshes(meshFilter.sharedMesh, component.sharedMaterials);
                }
            }
        }
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in base.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (Outline.registeredMeshes.Add(skinnedMeshRenderer.sharedMesh))
            {
                skinnedMeshRenderer.sharedMesh.uv4 = new Vector2[skinnedMeshRenderer.sharedMesh.vertexCount];
                this.CombineSubmeshes(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials);
            }
        }
    }

    // Token: 0x060002C9 RID: 713 RVA: 0x0000D6B0 File Offset: 0x0000B8B0
    private List<Vector3> SmoothNormals(Mesh mesh)
    {
        IEnumerable<IGrouping<Vector3, KeyValuePair<Vector3, int>>> enumerable = from pair in mesh.vertices.Select((Vector3 vertex, int index) => new KeyValuePair<Vector3, int>(vertex, index))
                                                                                 group pair by pair.Key;
        List<Vector3> list = new List<Vector3>(mesh.normals);
        foreach (IGrouping<Vector3, KeyValuePair<Vector3, int>> grouping in enumerable)
        {
            if (grouping.Count<KeyValuePair<Vector3, int>>() != 1)
            {
                Vector3 vector = Vector3.zero;
                foreach (KeyValuePair<Vector3, int> keyValuePair in grouping)
                {
                    vector += list[keyValuePair.Value];
                }
                vector.Normalize();
                foreach (KeyValuePair<Vector3, int> keyValuePair2 in grouping)
                {
                    list[keyValuePair2.Value] = vector;
                }
            }
        }
        return list;
    }

    // Token: 0x060002CA RID: 714 RVA: 0x0000D7F8 File Offset: 0x0000B9F8
    private void CombineSubmeshes(Mesh mesh, Material[] materials)
    {
        if (mesh.subMeshCount == 1)
        {
            return;
        }
        if (mesh.subMeshCount > materials.Length)
        {
            return;
        }
        int subMeshCount = mesh.subMeshCount;
        mesh.subMeshCount = subMeshCount + 1;
        mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1);
    }

    // Token: 0x060002CB RID: 715 RVA: 0x0000D840 File Offset: 0x0000BA40
    private void UpdateMaterialProperties()
    {
        this.outlineFillMaterial.SetColor("_OutlineColor", this.outlineColor);
        switch (this.outlineMode)
        {
            case Outline.Mode.OutlineAll:
                this.outlineMaskMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.OutlineVisible:
                this.outlineMaskMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_ZTest", 4f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.OutlineHidden:
                this.outlineMaskMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_ZTest", 5f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.OutlineAndSilhouette:
                this.outlineMaskMaterial.SetFloat("_ZTest", 4f);
                this.outlineFillMaterial.SetFloat("_ZTest", 8f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                return;
            case Outline.Mode.SilhouetteOnly:
                this.outlineMaskMaterial.SetFloat("_ZTest", 4f);
                this.outlineFillMaterial.SetFloat("_ZTest", 5f);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", 0f);
                return;
            default:
                return;
        }
    }

    // Token: 0x0400039C RID: 924
    private static HashSet<Mesh> registeredMeshes = new HashSet<Mesh>();

    // Token: 0x0400039D RID: 925
    [SerializeField]
    private Outline.Mode outlineMode;

    // Token: 0x0400039E RID: 926
    [SerializeField]
    private Color outlineColor = Color.white;

    // Token: 0x0400039F RID: 927
    [SerializeField]
    [Range(0f, 10f)]
    private float outlineWidth = 2f;

    // Token: 0x040003A0 RID: 928
    [Header("Optional")]
    [SerializeField]
    [Tooltip("Precompute enabled: Per-vertex calculations are performed in the editor and serialized with the object. Precompute disabled: Per-vertex calculations are performed at runtime in Awake(). This may cause a pause for large meshes.")]
    private bool precomputeOutline;

    // Token: 0x040003A1 RID: 929
    [SerializeField]
    [HideInInspector]
    private List<Mesh> bakeKeys = new List<Mesh>();

    // Token: 0x040003A2 RID: 930
    [SerializeField]
    [HideInInspector]
    private List<Outline.ListVector3> bakeValues = new List<Outline.ListVector3>();

    // Token: 0x040003A3 RID: 931
    private Renderer[] renderers;

    // Token: 0x040003A4 RID: 932
    private Material outlineMaskMaterial;

    // Token: 0x040003A5 RID: 933
    private Material outlineFillMaterial;

    // Token: 0x040003A6 RID: 934
    private bool needsUpdate;

    // Token: 0x02000104 RID: 260
    public enum Mode
    {
        // Token: 0x040005ED RID: 1517
        OutlineAll,
        // Token: 0x040005EE RID: 1518
        OutlineVisible,
        // Token: 0x040005EF RID: 1519
        OutlineHidden,
        // Token: 0x040005F0 RID: 1520
        OutlineAndSilhouette,
        // Token: 0x040005F1 RID: 1521
        SilhouetteOnly
    }

    // Token: 0x02000105 RID: 261
    [Serializable]
    private class ListVector3
    {
        // Token: 0x040005F2 RID: 1522
        public List<Vector3> data;
    }
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Token: 0x02000073 RID: 115
public class PatapimBoss : MonoBehaviour
{
    // Token: 0x17000007 RID: 7
    // (get) Token: 0x06000238 RID: 568 RVA: 0x0000AF4B File Offset: 0x0000914B
    private bool Stage2
    {
        get
        {
            return this.health <= this.stage2Health;
        }
    }

    // Token: 0x06000239 RID: 569 RVA: 0x0000AF60 File Offset: 0x00009160
    private void Awake()
    {
        this.agent = base.GetComponent<NavMeshAgent>();
        this.animator = base.GetComponentInChildren<Animator>();
        this.renderers = base.GetComponentsInChildren<Renderer>();
        this.agent.updateRotation = false;
        this.health = this.startHealth;
        this.damage = this.startDamage;
    }

    // Token: 0x0600023A RID: 570 RVA: 0x0000AFB5 File Offset: 0x000091B5
    private void Start()
    {
        this.agent.speed = this.startSpeed;
        this.healthSlider.gameObject.SetActive(true);
    }

    // Token: 0x0600023B RID: 571 RVA: 0x0000AFDC File Offset: 0x000091DC
    private void Update()
    {
        this.healthSlider.value = this.health / this.startHealth;
        bool flag = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        bool playerInAttack = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
        this.animator.SetBool("isWalking", this.agent.velocity.magnitude > 0.1f && !this.isAttacking);
        if (!flag)
        {
            this.Patrol();
            return;
        }
        this.ChaseOrAttack(playerInAttack);
    }

    // Token: 0x0600023C RID: 572 RVA: 0x0000B08C File Offset: 0x0000928C
    private void SetBossVisible(bool visible)
    {
        Renderer[] array = this.renderers;
        for (int i = 0; i < array.Length; i++)
        {
            array[i].enabled = visible;
        }
    }

    // Token: 0x0600023D RID: 573 RVA: 0x0000B0B8 File Offset: 0x000092B8
    private void Patrol()
    {
        if (this.isAttacking)
        {
            return;
        }
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if (Vector3.Distance(base.transform.position, this.walkPoint) < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x0600023E RID: 574 RVA: 0x0000B11C File Offset: 0x0000931C
    private void SearchWalkPoint()
    {
        Vector3 a = base.transform.position + new Vector3(Random.Range(-this.walkPointRange, this.walkPointRange), 0f, Random.Range(-this.walkPointRange, this.walkPointRange));
        if (Physics.Raycast(a + Vector3.up, Vector3.down, 2f, this.whatIsGround))
        {
            this.walkPoint = a;
            this.walkPointSet = true;
        }
    }

    // Token: 0x0600023F RID: 575 RVA: 0x0000B1A0 File Offset: 0x000093A0
    private void ChaseOrAttack(bool playerInAttack)
    {
        if (this.isAttacking)
        {
            return;
        }
        this.RotateTowards(this.player.position);
        if (playerInAttack)
        {
            this.AttackPlayer();
            return;
        }
        this.agent.isStopped = false;
        this.agent.SetDestination(this.player.position);
        if (Random.value < 0.01f)
        {
            base.StartCoroutine(this.RestAndAttack());
        }
    }

    // Token: 0x06000240 RID: 576 RVA: 0x0000B210 File Offset: 0x00009410
    private void RotateTowards(Vector3 target)
    {
        Vector3 vector = target - base.transform.position;
        vector.y = 0f;
        if (vector != Vector3.zero)
        {
            base.transform.rotation = Quaternion.Slerp(base.transform.rotation, Quaternion.LookRotation(vector), Time.deltaTime * 8f);
        }
    }

    // Token: 0x06000241 RID: 577 RVA: 0x0000B274 File Offset: 0x00009474
    private void AttackPlayer()
    {
        this.agent.SetDestination(base.transform.position);
        this.RotateTowards(this.player.position);
        if (this.alreadyAttacked)
        {
            return;
        }
        PlayerHealth component = this.player.GetComponent<PlayerHealth>();
        if (component != null)
        {
            component.TakeDamage((float)this.damage);
        }
        this.alreadyAttacked = true;
        base.Invoke("ResetAttack", this.timeBetweenAttacks);
        if (Random.value < 0.1f)
        {
            base.StartCoroutine(this.RestAndAttack());
        }
    }

    // Token: 0x06000242 RID: 578 RVA: 0x0000B305 File Offset: 0x00009505
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x06000243 RID: 579 RVA: 0x0000B30E File Offset: 0x0000950E
    private IEnumerator RestAndAttack()
    {
        if (this.isAttacking)
        {
            yield break;
        }
        this.isAttacking = true;
        this.agent.isStopped = true;
        this.animator.SetBool("isWalking", false);
        yield return new WaitForSeconds(this.Stage2 ? 0.6f : 1.1f);
        this.ChooseMoveset();
        yield break;
    }

    // Token: 0x06000244 RID: 580 RVA: 0x0000B320 File Offset: 0x00009520
    private void ChooseMoveset()
    {
        int maxExclusive = this.Stage2 ? 6 : 4;
        switch (Random.Range(0, maxExclusive))
        {
            case 0:
                base.StartCoroutine(this.TripleJump());
                return;
            case 1:
                base.StartCoroutine(this.BigJumpToPlayer());
                return;
            case 2:
                base.StartCoroutine(this.RandomSpecial());
                return;
            case 3:
                if (this.Stage2)
                {
                    base.StartCoroutine(this.SummonTrees());
                    return;
                }
                base.StartCoroutine(this.RandomSpecial());
                return;
            case 4:
                if (this.superAttackCount < 2 && Random.value < 0.25f)
                {
                    this.superAttackCount++;
                    base.StartCoroutine(this.SuperAttack());
                    return;
                }
                base.StartCoroutine(this.RandomSpecial());
                return;
            default:
                base.StartCoroutine(this.RandomSpecial());
                return;
        }
    }

    // Token: 0x06000245 RID: 581 RVA: 0x0000B3FB File Offset: 0x000095FB
    private IEnumerator TripleJump()
    {
        this.animator.SetTrigger("jumpWarn");
        yield return new WaitForSeconds(3f);
        int jumps = this.Stage2 ? 4 : 3;
        float height = this.Stage2 ? (this.jumpHeight * 0.7f) : (this.jumpHeight * 0.5f);
        int num;
        for (int i = 0; i < jumps; i = num + 1)
        {
            this.animator.SetTrigger("jump");
            yield return this.JumpArc(base.transform.position, base.transform.position, height, this.jumpDuration * 0.6f);
            Vector3 position = this.player.position;
            base.StartCoroutine(this.SpawnShockwaveLine(base.transform.position, position));
            yield return new WaitForSeconds(0.35f);
            num = i;
        }
        this.EndAttack();
        yield break;
    }

    // Token: 0x06000246 RID: 582 RVA: 0x0000B40A File Offset: 0x0000960A
    private IEnumerator BigJumpToPlayer()
    {
        Vector3 start = base.transform.position;
        Vector3 end = this.player.position;
        end.y = start.y;
        this.animator.SetTrigger("jumpWarn");
        yield return new WaitForSeconds(this.jumpWarnDelay);
        this.animator.SetTrigger("bigJump");
        yield return this.JumpArc(start, end + (end - start).normalized * 1.5f, this.Stage2 ? (this.jumpHeight * 1.2f) : this.jumpHeight, this.Stage2 ? (this.jumpDuration * 0.7f) : this.jumpDuration);
        Object.Instantiate<GameObject>(this.hugeShockwavePrefab, base.transform.position, Quaternion.identity);
        this.EndAttack();
        yield break;
    }

    // Token: 0x06000247 RID: 583 RVA: 0x0000B419 File Offset: 0x00009619
    private IEnumerator RandomSpecial()
    {
        this.animator.SetTrigger("summon");
        yield return new WaitForSeconds(1f);
        if (Random.value > 0.5f)
        {
            int count = this.Stage2 ? 10 : 7;
            int num;
            for (int i = 0; i < count; i = num + 1)
            {
                yield return this.SummonLightning();
                num = i;
            }
        }
        else
        {
            this.SpawnBombs(this.Stage2 ? 12 : 8);
        }
        this.EndAttack();
        yield break;
    }

    // Token: 0x06000248 RID: 584 RVA: 0x0000B428 File Offset: 0x00009628
    private IEnumerator SummonTrees()
    {
        this.animator.SetTrigger("summon");
        yield return new WaitForSeconds(1.5f);
        int num;
        for (int i = 0; i < 6; i = num + 1)
        {
            Vector3 a = this.RandomPointInBox(new Vector3(18f, 0f, 18f));
            Object.Instantiate<GameObject>(this.fallingTreePrefab, a + Vector3.up * 15f, Quaternion.identity);
            yield return new WaitForSeconds(0.3f);
            num = i;
        }
        this.EndAttack();
        yield break;
    }

    // Token: 0x06000249 RID: 585 RVA: 0x0000B437 File Offset: 0x00009637
    private IEnumerator SuperAttack()
    {
        this.agent.isStopped = true;
        this.agent.updatePosition = false;
        Vector3 targetPos = this.player.position;
        GameObject gameObject = Object.Instantiate<GameObject>(this.rocketTrailPrefab, base.transform.position, Quaternion.LookRotation(Vector3.up));
        gameObject.transform.SetParent(base.transform, true);
        gameObject.transform.localRotation = Quaternion.identity;
        this.animator.SetTrigger("jump");
        float riseTime = 1.5f;
        float t = 0f;
        Vector3 start = base.transform.position;
        Vector3 upTarget = start + Vector3.up * 50f;
        while (t < riseTime)
        {
            base.transform.position = Vector3.Lerp(start, upTarget, t / riseTime);
            t += Time.deltaTime;
            yield return null;
        }
        this.SetBossVisible(false);
        yield return new WaitForSeconds(5f);
        base.transform.position = targetPos + Vector3.up * 40f;
        this.SetBossVisible(true);
        this.animator.SetTrigger("slam");
        float fallTime = 0.6f;
        float f = 0f;
        Vector3 fallStart = base.transform.position;
        while (f < fallTime)
        {
            base.transform.position = Vector3.Lerp(fallStart, targetPos, f / fallTime);
            f += Time.deltaTime;
            yield return null;
        }
        base.transform.position = targetPos;
        this.agent.updatePosition = true;
        NavMeshHit navMeshHit;
        if (NavMesh.SamplePosition(targetPos, out navMeshHit, 3f, -1))
        {
            this.agent.Warp(navMeshHit.position);
        }
        else
        {
            this.agent.Warp(targetPos);
        }
        this.agent.isStopped = false;
        Object.Instantiate<GameObject>(this.massiveShockwavePrefab, base.transform.position, Quaternion.identity);
        this.EndAttack();
        yield break;
    }

    // Token: 0x0600024A RID: 586 RVA: 0x0000B446 File Offset: 0x00009646
    private IEnumerator JumpArc(Vector3 start, Vector3 end, float height, float duration)
    {
        this.agent.isStopped = true;
        this.agent.updatePosition = false;
        float time = 0f;
        while (time < duration)
        {
            float num = time / duration;
            Vector3 vector = Vector3.Lerp(start, end, num);
            float num2 = Mathf.Sin(num * 3.1415927f) * height;
            base.transform.position = new Vector3(vector.x, start.y + num2, vector.z);
            time += Time.deltaTime;
            yield return null;
        }
        base.transform.position = end;
        this.agent.updatePosition = true;
        this.agent.Warp(end);
        this.agent.isStopped = false;
        yield break;
    }

    // Token: 0x0600024B RID: 587 RVA: 0x0000B474 File Offset: 0x00009674
    private void SpawnBombs(int directions)
    {
        float num = 360f / (float)directions;
        for (int i = 0; i < directions; i++)
        {
            float y = num * (float)i;
            Vector3 a = Quaternion.Euler(0f, y, 0f) * Vector3.forward;
            Vector3 position = base.transform.position + a * this.bombSpawnRadius;
            Rigidbody componentInChildren = Object.Instantiate<GameObject>(this.bombPrefab, position, Quaternion.identity).GetComponentInChildren<Rigidbody>();
            if (componentInChildren != null)
            {
                componentInChildren.velocity = a * this.bombForce;
            }
        }
        Object.Instantiate<GameObject>(this.magicSound, base.transform.position, Quaternion.identity);
    }

    // Token: 0x0600024C RID: 588 RVA: 0x0000B526 File Offset: 0x00009726
    private IEnumerator SummonLightning()
    {
        Vector3 pos = this.RandomPointInBox(new Vector3(15f, 0f, 15f));
        Object.Instantiate<GameObject>(this.preLightningPrefab, pos, Quaternion.identity);
        yield return new WaitForSeconds(1f);
        Object.Instantiate<GameObject>(this.lightningPrefab, pos, Quaternion.identity);
        Object.Instantiate<GameObject>(this.magicSound, pos, Quaternion.identity);
        yield break;
    }

    // Token: 0x0600024D RID: 589 RVA: 0x0000B535 File Offset: 0x00009735
    private Vector3 RandomPointInBox(Vector3 size)
    {
        return this.player.position + new Vector3((Random.value - 0.5f) * size.x, -1f, (Random.value - 0.5f) * size.z);
    }

    // Token: 0x0600024E RID: 590 RVA: 0x0000B578 File Offset: 0x00009778
    private void EndAttack()
    {
        base.StopAllCoroutines();
        this.isAttacking = false;
        this.alreadyAttacked = false;
        this.agent.isStopped = false;
        this.agent.ResetPath();
        this.animator.ResetTrigger("jump");
        this.animator.ResetTrigger("bigJump");
        this.animator.ResetTrigger("slam");
    }

    // Token: 0x0600024F RID: 591 RVA: 0x0000B5E0 File Offset: 0x000097E0
    private IEnumerator SpawnShockwaveLine(Vector3 startPos, Vector3 targetPos)
    {
        Vector3 dir = (targetPos - startPos).normalized;
        float spacing = 2.5f;
        int num;
        for (int i = 1; i <= 5; i = num + 1)
        {
            Vector3 position = startPos + dir * spacing * (float)i;
            Object.Instantiate<GameObject>(this.shockwavePrefab, position, Quaternion.LookRotation(dir));
            yield return new WaitForSeconds(0.2f);
            num = i;
        }
        yield break;
    }

    // Token: 0x06000250 RID: 592 RVA: 0x0000B600 File Offset: 0x00009800
    public void TakeDamage(int dmg)
    {
        this.health -= (float)dmg;
        if (this.Stage2 && !this.stage2Triggered)
        {
            this.stage2Triggered = true;
            this.superAttackCount++;
            base.StopAllCoroutines();
            base.StartCoroutine(this.SuperAttack());
            return;
        }
        if (this.health <= 0f)
        {
            base.Invoke("Die", 0.5f);
        }
    }

    // Token: 0x06000251 RID: 593 RVA: 0x0000B674 File Offset: 0x00009874
    private void Die()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.finalMushroom, base.transform.position, Quaternion.identity);
        this.healthSlider.gameObject.SetActive(false);
        MusicManager.instance.EnableMusic(0);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x06000252 RID: 594 RVA: 0x0000B6E0 File Offset: 0x000098E0
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x040002D4 RID: 724
    [Header("Detection")]
    public LayerMask whatIsGround;

    // Token: 0x040002D5 RID: 725
    [Header("Detection")]
    public LayerMask whatIsPlayer;

    // Token: 0x040002D6 RID: 726
    public float sightRange = 20f;

    // Token: 0x040002D7 RID: 727
    public float attackRange = 6f;

    // Token: 0x040002D8 RID: 728
    [Header("Patrol")]
    public float walkPointRange = 10f;

    // Token: 0x040002D9 RID: 729
    private Vector3 walkPoint;

    // Token: 0x040002DA RID: 730
    private bool walkPointSet;

    // Token: 0x040002DB RID: 731
    [Header("Stats & Combat")]
    public float startHealth = 100f;

    // Token: 0x040002DC RID: 732
    public int startDamage = 15;

    // Token: 0x040002DD RID: 733
    public float startSpeed = 3.5f;

    // Token: 0x040002DE RID: 734
    public float timeBetweenAttacks = 1.2f;

    // Token: 0x040002DF RID: 735
    private float health;

    // Token: 0x040002E0 RID: 736
    private int damage;

    // Token: 0x040002E1 RID: 737
    private bool isAttacking;

    // Token: 0x040002E2 RID: 738
    private bool alreadyAttacked;

    // Token: 0x040002E3 RID: 739
    private Renderer[] renderers;

    // Token: 0x040002E4 RID: 740
    [Header("Stage Settings")]
    public float stage2Health = 45f;

    // Token: 0x040002E5 RID: 741
    [Header("Prefabs")]
    public GameObject shockwavePrefab;

    // Token: 0x040002E6 RID: 742
    [Header("Prefabs")]
    public GameObject hugeShockwavePrefab;

    // Token: 0x040002E7 RID: 743
    [Header("Prefabs")]
    public GameObject bombPrefab;

    // Token: 0x040002E8 RID: 744
    public GameObject massiveShockwavePrefab;

    // Token: 0x040002E9 RID: 745
    public GameObject lightningPrefab;

    // Token: 0x040002EA RID: 746
    public GameObject preLightningPrefab;

    // Token: 0x040002EB RID: 747
    public GameObject fallingTreePrefab;

    // Token: 0x040002EC RID: 748
    public GameObject rocketTrailPrefab;

    // Token: 0x040002ED RID: 749
    public GameObject deathEffect;

    // Token: 0x040002EE RID: 750
    [Header("References")]
    public Transform player;

    // Token: 0x040002EF RID: 751
    public Slider healthSlider;

    // Token: 0x040002F0 RID: 752
    [Header("Jump Settings")]
    public float jumpHeight = 8f;

    // Token: 0x040002F1 RID: 753
    public float jumpDuration = 0.8f;

    // Token: 0x040002F2 RID: 754
    public float jumpWarnDelay = 0.6f;

    // Token: 0x040002F3 RID: 755
    [Header("Bomb Settings")]
    public float bombSpawnRadius = 3.5f;

    // Token: 0x040002F4 RID: 756
    public float bombForce = 8f;

    // Token: 0x040002F5 RID: 757
    public GameObject magicSound;

    // Token: 0x040002F6 RID: 758
    public GameObject finalMushroom;

    // Token: 0x040002F7 RID: 759
    private NavMeshAgent agent;

    // Token: 0x040002F8 RID: 760
    private Animator animator;

    // Token: 0x040002F9 RID: 761
    private bool stage2Triggered;

    // Token: 0x040002FA RID: 762
    private int superAttackCount;
}

using System;
using UnityEngine;
using UnityEngine.Video;

// Token: 0x02000048 RID: 72
public class PauseMenu : MonoBehaviour
{
    // Token: 0x06000178 RID: 376 RVA: 0x00007E52 File Offset: 0x00006052
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!this.pausePanel.activeInHierarchy)
            {
                this.OnPause();
                return;
            }
            this.OnResume();
        }
    }

    // Token: 0x06000179 RID: 377 RVA: 0x00007E78 File Offset: 0x00006078
    public void OnPause()
    {
        this.pausePanel.SetActive(true);
        this.lockPanel.SetActive(false);
        this.UnlockCursor();
        VideoPlayer[] array = Object.FindObjectsOfType<VideoPlayer>();
        for (int i = 0; i < array.Length; i++)
        {
            array[i].Pause();
        }
    }

    // Token: 0x0600017A RID: 378 RVA: 0x00007EC0 File Offset: 0x000060C0
    public void OnResume()
    {
        this.pausePanel.SetActive(false);
        this.LockCursor();
        VideoPlayer[] array = Object.FindObjectsOfType<VideoPlayer>();
        for (int i = 0; i < array.Length; i++)
        {
            array[i].Play();
        }
    }

    // Token: 0x0600017B RID: 379 RVA: 0x00007EFB File Offset: 0x000060FB
    public void LockCursor()
    {
        CursorController.LockCursor();
        Time.timeScale = 1f;
    }

    // Token: 0x0600017C RID: 380 RVA: 0x00007F0C File Offset: 0x0000610C
    public void UnlockCursor()
    {
        CursorController.UnlockCursor();
        Time.timeScale = 0f;
    }

    // Token: 0x04000203 RID: 515
    public GameObject pausePanel;

    // Token: 0x04000204 RID: 516
    public GameObject lockPanel;
}

using System;
using UnityEngine;

// Token: 0x02000027 RID: 39
public class Pier : MonoBehaviour
{
    // Token: 0x060000E0 RID: 224 RVA: 0x000056FE File Offset: 0x000038FE
    private void Start()
    {
        this.LoadPresence();
        this.CheckPresence();
    }

    // Token: 0x060000E1 RID: 225 RVA: 0x0000570C File Offset: 0x0000390C
    private void LoadPresence()
    {
        this.presence = PlayerPrefs.GetString("Pier", "true");
    }

    // Token: 0x060000E2 RID: 226 RVA: 0x00005723 File Offset: 0x00003923
    public void SavePresence()
    {
        PlayerPrefs.SetString("Pier", this.presence);
        this.CheckPresence();
    }

    // Token: 0x060000E3 RID: 227 RVA: 0x0000573C File Offset: 0x0000393C
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.boat.pierPos = this.pierPos;
            this.boat.pierIsNear = true;
            this.boat.boatText.gameObject.SetActive(true);
        }
    }

    // Token: 0x060000E4 RID: 228 RVA: 0x00005789 File Offset: 0x00003989
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.boat.pierIsNear = false;
            this.boat.boatText.gameObject.SetActive(false);
        }
    }

    // Token: 0x060000E5 RID: 229 RVA: 0x000057BC File Offset: 0x000039BC
    public void CheckPresence()
    {
        if (this.presence == "false")
        {
            this.boat.pierIsNear = false;
            this.boat.boatText.gameObject.SetActive(false);
            base.gameObject.SetActive(false);
            return;
        }
        if (this.presence == "true")
        {
            base.gameObject.SetActive(true);
        }
    }

    // Token: 0x04000138 RID: 312
    public BoatSwitch boat;

    // Token: 0x04000139 RID: 313
    public Transform pierPos;

    // Token: 0x0400013A RID: 314
    public bool lastPier;

    // Token: 0x0400013B RID: 315
    public string presence = "true";
}

using System;
using UnityEngine;

// Token: 0x02000074 RID: 116
public class PlaneAttack : MonoBehaviour
{
    // Token: 0x06000254 RID: 596 RVA: 0x0000B7CF File Offset: 0x000099CF
    public void EnableBombing(bool enable)
    {
        this.bombingEnabled = enable;
    }

    // Token: 0x06000255 RID: 597 RVA: 0x0000B7D8 File Offset: 0x000099D8
    public void StartBombing(Transform target, Vector2 min, Vector2 max)
    {
        this.player = target;
        this.waterMin = min;
        this.waterMax = max;
        this.bombsPerDrop = 1;
        this.difficultyTimer = 0f;
        this.active = true;
        base.gameObject.SetActive(true);
        this.PickNewDiagonal();
    }

    // Token: 0x06000256 RID: 598 RVA: 0x0000B825 File Offset: 0x00009A25
    private void Update()
    {
        if (!this.active)
        {
            return;
        }
        this.Move();
        if (this.state == PlaneAttack.State.DiagonalAttack && this.bombingEnabled)
        {
            this.Bombing();
        }
        this.IncreaseDifficulty();
    }

    // Token: 0x06000257 RID: 599 RVA: 0x0000B854 File Offset: 0x00009A54
    private void Move()
    {
        Vector3 vector = (this.state == PlaneAttack.State.DiagonalAttack) ? this.diagEnd : this.sideTarget;
        base.transform.position = Vector3.MoveTowards(base.transform.position, vector, this.speed * Time.deltaTime);
        Vector3 normalized = (vector - base.transform.position).normalized;
        if (normalized != Vector3.zero)
        {
            base.transform.forward = normalized;
        }
        if (Vector3.Distance(base.transform.position, vector) < 0.1f)
        {
            if (this.state == PlaneAttack.State.DiagonalAttack)
            {
                this.StartSideTravel();
                return;
            }
            this.PickNewDiagonal();
        }
    }

    // Token: 0x06000258 RID: 600 RVA: 0x0000B900 File Offset: 0x00009B00
    private void PickNewDiagonal()
    {
        bool flag = Random.value > 0.5f;
        bool flag2 = Random.value > 0.5f;
        this.diagStart = new Vector3(flag ? this.waterMin.x : this.waterMax.x, this.height, flag2 ? this.waterMin.y : this.waterMax.y);
        this.diagEnd = new Vector3(flag ? this.waterMax.x : this.waterMin.x, this.height, flag2 ? this.waterMax.y : this.waterMin.y);
        base.transform.position = this.diagStart;
        this.dropTimer = 0f;
        this.state = PlaneAttack.State.DiagonalAttack;
    }

    // Token: 0x06000259 RID: 601 RVA: 0x0000B9D8 File Offset: 0x00009BD8
    private void StartSideTravel()
    {
        this.sideTarget = base.transform.position;
        if (Random.value > 0.5f)
        {
            this.sideTarget.x = ((base.transform.position.x < (this.waterMin.x + this.waterMax.x) / 2f) ? this.waterMax.x : this.waterMin.x);
        }
        else
        {
            this.sideTarget.z = ((base.transform.position.z < (this.waterMin.y + this.waterMax.y) / 2f) ? this.waterMax.y : this.waterMin.y);
        }
        this.state = PlaneAttack.State.SideTravel;
    }

    // Token: 0x0600025A RID: 602 RVA: 0x0000BAB4 File Offset: 0x00009CB4
    private void Bombing()
    {
        this.dropTimer += Time.deltaTime;
        if (this.dropTimer < this.dropInterval)
        {
            return;
        }
        this.dropTimer = 0f;
        for (int i = 0; i < this.bombsPerDrop; i++)
        {
            Vector2 vector = Random.insideUnitCircle * this.dropRadius;
            Vector3 position = new Vector3(base.transform.position.x + vector.x, base.transform.position.y, base.transform.position.z + vector.y);
            Object.Instantiate<GameObject>(this.bombPrefab, position, Quaternion.identity);
        }
        float value = Random.value;
        if (value > 0.8f)
        {
            Object.Instantiate<GameObject>(this.soundOgurec1, base.transform.position, Quaternion.identity);
            return;
        }
        if (value > 0.6f)
        {
            Object.Instantiate<GameObject>(this.soundOgurec2, base.transform.position, Quaternion.identity);
        }
    }

    // Token: 0x0600025B RID: 603 RVA: 0x0000BBB8 File Offset: 0x00009DB8
    private void IncreaseDifficulty()
    {
        this.difficultyTimer += Time.deltaTime;
        if (this.difficultyTimer >= this.bombIncreaseInterval)
        {
            this.difficultyTimer = 0f;
            this.bombsPerDrop = Mathf.Min(this.maxBombsPerDrop, this.bombsPerDrop + 1);
        }
    }

    // Token: 0x040002FB RID: 763
    [Header("Movement")]
    public float speed = 15f;

    // Token: 0x040002FC RID: 764
    public float height = 12f;

    // Token: 0x040002FD RID: 765
    [Header("Bombing")]
    public GameObject bombPrefab;

    // Token: 0x040002FE RID: 766
    public float dropInterval = 1.2f;

    // Token: 0x040002FF RID: 767
    public float dropRadius = 3f;

    // Token: 0x04000300 RID: 768
    public int bombsPerDrop = 1;

    // Token: 0x04000301 RID: 769
    public int maxBombsPerDrop = 5;

    // Token: 0x04000302 RID: 770
    public GameObject soundOgurec1;

    // Token: 0x04000303 RID: 771
    public GameObject soundOgurec2;

    // Token: 0x04000304 RID: 772
    [Header("Difficulty")]
    public float bombIncreaseInterval = 18f;

    // Token: 0x04000305 RID: 773
    public Transform lookTarget;

    // Token: 0x04000306 RID: 774
    private Transform player;

    // Token: 0x04000307 RID: 775
    private Vector2 waterMin;

    // Token: 0x04000308 RID: 776
    private Vector2 waterMax;

    // Token: 0x04000309 RID: 777
    private PlaneAttack.State state;

    // Token: 0x0400030A RID: 778
    private Vector3 diagStart;

    // Token: 0x0400030B RID: 779
    private Vector3 diagEnd;

    // Token: 0x0400030C RID: 780
    private Vector3 sideTarget;

    // Token: 0x0400030D RID: 781
    private float dropTimer;

    // Token: 0x0400030E RID: 782
    private float difficultyTimer;

    // Token: 0x0400030F RID: 783
    private bool bombingEnabled = true;

    // Token: 0x04000310 RID: 784
    private bool active;

    // Token: 0x020000FA RID: 250
    private enum State
    {
        // Token: 0x040005C2 RID: 1474
        DiagonalAttack,
        // Token: 0x040005C3 RID: 1475
        SideTravel
    }
}

using System;
using Cinemachine;
using UnityEngine;

// Token: 0x0200003A RID: 58
public class PlayerCamera : MonoBehaviour
{
    // Token: 0x06000136 RID: 310 RVA: 0x00006F35 File Offset: 0x00005135
    private void Start()
    {
        CursorController.LockCursor();
    }

    // Token: 0x06000137 RID: 311 RVA: 0x00006F3C File Offset: 0x0000513C
    private void Update()
    {
        if (!this.player.isStopped)
        {
            float num = Input.GetAxisRaw("Mouse X") * Time.deltaTime * this.sensitivityX;
            float num2 = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * this.sensitivityY;
            this.yRotation += num;
            this.xRotation -= num2;
            this.xRotation = Mathf.Clamp(this.xRotation, -90f, 90f);
            this.virtualCamera.transform.rotation = Quaternion.Euler(this.xRotation, this.yRotation, 0f);
            this.orientation.rotation = Quaternion.Euler(0f, this.yRotation, 0f);
        }
    }

    // Token: 0x06000138 RID: 312 RVA: 0x00007006 File Offset: 0x00005206
    public void FlipPlayer()
    {
        this.yRotation += 180f;
    }

    // Token: 0x040001AB RID: 427
    public float sensitivityX;

    // Token: 0x040001AC RID: 428
    public float sensitivityY;

    // Token: 0x040001AD RID: 429
    public PlayerController player;

    // Token: 0x040001AE RID: 430
    public Transform orientation;

    // Token: 0x040001AF RID: 431
    public bool test;

    // Token: 0x040001B0 RID: 432
    public CinemachineVirtualCamera virtualCamera;

    // Token: 0x040001B1 RID: 433
    private float xRotation;

    // Token: 0x040001B2 RID: 434
    private float yRotation;
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Token: 0x0200003B RID: 59
public class PlayerController : MonoBehaviour
{
    // Token: 0x0600013A RID: 314 RVA: 0x00007024 File Offset: 0x00005224
    private void Start()
    {
        this.rb = base.GetComponent<Rigidbody>();
        this.rb.freezeRotation = true;
        this.readyToJump = true;
        if (GravityController.instance != null)
        {
            GravityController.instance.player = this;
            GravityController.instance.CheckGravity();
        }
        this.LoadPosition();
    }

    // Token: 0x0600013B RID: 315 RVA: 0x00007078 File Offset: 0x00005278
    private void Update()
    {
        if (!this.isStopped)
        {
            this.grounded = Physics.Raycast(base.transform.position, Vector3.down, this.groundedValue * 0.5f + 0.01f, this.whatIsGround);
            this.MyInput();
            this.SpeedControl();
            if (this.grounded)
            {
                this.rb.drag = this.groundDrag;
                return;
            }
            this.rb.drag = 0f;
        }
    }

    // Token: 0x0600013C RID: 316 RVA: 0x000070FB File Offset: 0x000052FB
    private void FixedUpdate()
    {
        if (!this.isStopped)
        {
            this.MovePlayer();
        }
    }

    // Token: 0x0600013D RID: 317 RVA: 0x0000710C File Offset: 0x0000530C
    private void MyInput()
    {
        this.horizontalInput = Input.GetAxisRaw("Horizontal");
        this.verticalInput = Input.GetAxisRaw("Vertical");
        if (Input.GetKey(this.jumpKey) && this.readyToJump && this.grounded)
        {
            this.readyToJump = false;
            this.Jump();
            base.Invoke("ResetJump", this.jumpCooldown);
        }
    }

    // Token: 0x0600013E RID: 318 RVA: 0x00007174 File Offset: 0x00005374
    private void MovePlayer()
    {
        this.moveDirection = this.orientation.forward * this.verticalInput + this.orientation.right * this.horizontalInput;
        if (this.grounded)
        {
            this.rb.AddForce(this.moveDirection.normalized * this.moveSpeed * 10f, ForceMode.Force);
            return;
        }
        if (!this.grounded)
        {
            this.rb.AddForce(this.moveDirection.normalized * this.moveSpeed * 10f * this.airMultiplier, ForceMode.Force);
        }
    }

    // Token: 0x0600013F RID: 319 RVA: 0x0000722C File Offset: 0x0000542C
    private void SpeedControl()
    {
        Vector3 vector = new Vector3(this.rb.velocity.x, 0f, this.rb.velocity.z);
        if (vector.magnitude > this.moveSpeed)
        {
            Vector3 vector2 = vector.normalized * this.moveSpeed;
            this.rb.velocity = new Vector3(vector2.x, this.rb.velocity.y, vector2.z);
        }
    }

    // Token: 0x06000140 RID: 320 RVA: 0x000072B4 File Offset: 0x000054B4
    private void Jump()
    {
        this.rb.velocity = new Vector3(this.rb.velocity.x, 0f, this.rb.velocity.z);
        this.rb.AddForce(base.transform.up * this.jumpForce, ForceMode.Impulse);
    }

    // Token: 0x06000141 RID: 321 RVA: 0x00007318 File Offset: 0x00005518
    private void ResetJump()
    {
        this.readyToJump = true;
    }

    // Token: 0x06000142 RID: 322 RVA: 0x00007324 File Offset: 0x00005524
    public void SavePosition()
    {
        this.x = base.transform.position.x;
        this.y = base.transform.position.y;
        this.z = base.transform.position.z;
        if (SceneManager.GetActiveScene().name == this.startSceneName)
        {
            PlayerPrefs.SetFloat("x", this.x);
            PlayerPrefs.SetFloat("y", this.y);
            PlayerPrefs.SetFloat("z", this.z);
        }
        else
        {
            PlayerPrefs.SetFloat("xMoon", this.x);
            PlayerPrefs.SetFloat("yMoon", this.y);
            PlayerPrefs.SetFloat("zMoon", this.z);
        }
        PlayerPrefs.SetString("Gun", this.hasGun);
        PlayerPrefs.SetString("Kotli", this.hasKotli);
    }

    // Token: 0x06000143 RID: 323 RVA: 0x00007410 File Offset: 0x00005610
    public void LoadPosition()
    {
        if (SceneManager.GetActiveScene().name == this.startSceneName)
        {
            this.x = PlayerPrefs.GetFloat("x", 402.57f);
            this.y = PlayerPrefs.GetFloat("y", -15.92f);
            this.z = PlayerPrefs.GetFloat("z", 647.69f);
        }
        else
        {
            this.x = PlayerPrefs.GetFloat("xMoon", 2.931f);
            this.y = PlayerPrefs.GetFloat("yMoon", 5.168f);
            this.z = PlayerPrefs.GetFloat("zMoon", -8.655f);
        }
        this.hasGun = PlayerPrefs.GetString("Gun", "false");
        this.hasKotli = PlayerPrefs.GetString("Kotli", "false");
        this.magazineSize = PlayerPrefs.GetInt("Magazine", 8);
        Vector3 position = new Vector3(this.x, this.y, this.z);
        if (this.hasGun == "true")
        {
            this.gun.SetActive(true);
        }
        else
        {
            this.gun.SetActive(false);
        }
        base.transform.position = position;
        this.orientation.transform.rotation = new Quaternion(0f, 180f, 0f, 0f);
    }

    // Token: 0x06000144 RID: 324 RVA: 0x0000756C File Offset: 0x0000576C
    public void SetupDialogue(bool mode)
    {
        this.isStopped = mode;
        if (mode)
        {
            CursorController.UnlockCursor();
            return;
        }
        CursorController.LockCursor();
    }

    // Token: 0x040001B3 RID: 435
    [Header("Movement")]
    public float moveSpeed;

    // Token: 0x040001B4 RID: 436
    public Transform orientation;

    // Token: 0x040001B5 RID: 437
    public float groundDrag;

    // Token: 0x040001B6 RID: 438
    public float groundedValue;

    // Token: 0x040001B7 RID: 439
    public float jumpForce;

    // Token: 0x040001B8 RID: 440
    public float jumpCooldown;

    // Token: 0x040001B9 RID: 441
    public float airMultiplier;

    // Token: 0x040001BA RID: 442
    public float x;

    // Token: 0x040001BB RID: 443
    public float y;

    // Token: 0x040001BC RID: 444
    public float z;

    // Token: 0x040001BD RID: 445
    public string hasGun = "false";

    // Token: 0x040001BE RID: 446
    public string hasKotli = "false";

    // Token: 0x040001BF RID: 447
    public int magazineSize;

    // Token: 0x040001C0 RID: 448
    public GameObject gun;

    // Token: 0x040001C1 RID: 449
    public GameObject aim;

    // Token: 0x040001C2 RID: 450
    public GameObject gunText;

    // Token: 0x040001C3 RID: 451
    private bool readyToJump;

    // Token: 0x040001C4 RID: 452
    [Header("Ground Check")]
    public float playerHeight;

    // Token: 0x040001C5 RID: 453
    public LayerMask whatIsGround;

    // Token: 0x040001C6 RID: 454
    public bool grounded;

    // Token: 0x040001C7 RID: 455
    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;

    // Token: 0x040001C8 RID: 456
    private float horizontalInput;

    // Token: 0x040001C9 RID: 457
    private float verticalInput;

    // Token: 0x040001CA RID: 458
    [HideInInspector]
    public bool isStopped;

    // Token: 0x040001CB RID: 459
    [Header("Scenes")]
    public string startSceneName;

    // Token: 0x040001CC RID: 460
    private Vector3 moveDirection;

    // Token: 0x040001CD RID: 461
    private Rigidbody rb;
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Token: 0x0200003C RID: 60
public class PlayerHealth : MonoBehaviour
{
    // Token: 0x06000146 RID: 326 RVA: 0x000075A9 File Offset: 0x000057A9
    private void Start()
    {
        this.currentHealth = this.maxHealth;
        this.currectHealCooldown = this.healCooldownAfterDamage;
    }

    // Token: 0x06000147 RID: 327 RVA: 0x000075C4 File Offset: 0x000057C4
    private void Update()
    {
        this.bloodOverlay.color = new Color(this.bloodOverlay.color.r, this.bloodOverlay.color.g, this.bloodOverlay.color.b, 1f - this.currentHealth / this.maxHealth);
        if (this.currectHealCooldown > 0f)
        {
            this.currectHealCooldown -= Time.deltaTime;
        }
        else
        {
            this.canHeal = true;
        }
        if (Time.realtimeSinceStartup < this.nextHealthUpdateTime)
        {
            return;
        }
        this.nextHealthUpdateTime = Time.realtimeSinceStartup + this.healSpeed;
        if (this.currentHealth < this.maxHealth && this.canHeal)
        {
            this.currentHealth += this.healAmount;
        }
    }

    // Token: 0x06000148 RID: 328 RVA: 0x00007698 File Offset: 0x00005898
    public void TakeDamage(float damage)
    {
        this.canHeal = false;
        this.currectHealCooldown = this.healCooldownAfterDamage;
        this.currentHealth -= damage;
        if (this.currentHealth <= 0f)
        {
            Object.FindObjectOfType<MusicManager>().EnableMusic(0);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // Token: 0x040001CE RID: 462
    public Image bloodOverlay;

    // Token: 0x040001CF RID: 463
    public float maxHealth = 100f;

    // Token: 0x040001D0 RID: 464
    public float currentHealth = 100f;

    // Token: 0x040001D1 RID: 465
    private float nextHealthUpdateTime;

    // Token: 0x040001D2 RID: 466
    public float healSpeed = 1f;

    // Token: 0x040001D3 RID: 467
    public float healAmount = 1f;

    // Token: 0x040001D4 RID: 468
    public float healCooldownAfterDamage = 5f;

    // Token: 0x040001D5 RID: 469
    public float currectHealCooldown;

    // Token: 0x040001D6 RID: 470
    private bool canHeal = true;
}

using System;
using UnityEngine;

// Token: 0x0200008C RID: 140
public class PlayerMovement : MonoBehaviour
{
    // Token: 0x060002B7 RID: 695 RVA: 0x0000D174 File Offset: 0x0000B374
    private void Update()
    {
        this.isGrounded = Physics.CheckSphere(this.groundCheck.position, this.groundDistance, this.groundMask);
        if (this.isGrounded && this.velocity.y < 0f)
        {
            this.velocity.y = -2f;
        }
        if (Input.GetKey("left shift") && this.isGrounded)
        {
            this.speed = 10f;
        }
        else
        {
            this.speed = 5f;
        }
        float axis = Input.GetAxis("Horizontal");
        float axis2 = Input.GetAxis("Vertical");
        Vector3 a = base.transform.right * axis + base.transform.forward * axis2;
        this.controller.Move(a * this.speed * Time.deltaTime);
        if (Input.GetButtonDown("Jump") && this.isGrounded)
        {
            this.velocity.y = Mathf.Sqrt(this.jumpHeight * -2f * this.gravity);
        }
        this.velocity.y = this.velocity.y + this.gravity * Time.deltaTime;
        this.controller.Move(this.velocity * Time.deltaTime);
    }

    // Token: 0x04000393 RID: 915
    public CharacterController controller;

    // Token: 0x04000394 RID: 916
    public float speed = 5f;

    // Token: 0x04000395 RID: 917
    public float gravity = -9.18f;

    // Token: 0x04000396 RID: 918
    public float jumpHeight = 3f;

    // Token: 0x04000397 RID: 919
    public Transform groundCheck;

    // Token: 0x04000398 RID: 920
    public float groundDistance = 0.4f;

    // Token: 0x04000399 RID: 921
    public LayerMask groundMask;

    // Token: 0x0400039A RID: 922
    private Vector3 velocity;

    // Token: 0x0400039B RID: 923
    private bool isGrounded;
}

using System;
using UnityEngine;

// Token: 0x02000066 RID: 102
public class ProjectileAddon : MonoBehaviour
{
    // Token: 0x060001F3 RID: 499 RVA: 0x00009D62 File Offset: 0x00007F62
    private void Start()
    {
        this.rb = base.GetComponent<Rigidbody>();
    }

    // Token: 0x060001F4 RID: 500 RVA: 0x00009D70 File Offset: 0x00007F70
    private void OnCollisionEnter(Collision collision)
    {
        if (this.targetHit)
        {
            return;
        }
        this.targetHit = true;
        if (collision.gameObject.GetComponent<BasicEnemy>() != null)
        {
            collision.gameObject.GetComponent<BasicEnemy>().TakeDamage(this.damage);
            Object.Destroy(base.gameObject);
        }
        this.rb.isKinematic = true;
        base.transform.SetParent(collision.transform);
    }

    // Token: 0x04000279 RID: 633
    public int damage;

    // Token: 0x0400027A RID: 634
    private Rigidbody rb;

    // Token: 0x0400027B RID: 635
    private bool targetHit;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000017 RID: 23
public class ProjectileGun : MonoBehaviour
{
    // Token: 0x06000082 RID: 130 RVA: 0x00003F32 File Offset: 0x00002132
    private void Awake()
    {
        this._animator = base.GetComponent<Animator>();
        this.bulletsLeft = this.magazineSize;
        this.readyToShoot = true;
    }

    // Token: 0x06000083 RID: 131 RVA: 0x00003F53 File Offset: 0x00002153
    private void Update()
    {
        this.MyInput();
    }

    // Token: 0x06000084 RID: 132 RVA: 0x00003F5C File Offset: 0x0000215C
    private void MyInput()
    {
        if (this.allowButtonHold)
        {
            this.shooting = Input.GetKey(KeyCode.Mouse0);
        }
        else
        {
            this.shooting = Input.GetKeyDown(KeyCode.Mouse0);
        }
        if (Input.GetKeyDown(KeyCode.R) && this.bulletsLeft < this.magazineSize && !this.reloading)
        {
            this.Reload();
        }
        if (this.readyToShoot && this.shooting && !this.reloading && this.bulletsLeft <= 0)
        {
            this.Reload();
        }
        if (this.readyToShoot && this.shooting && !this.reloading && this.bulletsLeft > 0)
        {
            this.bulletsShot = 0;
            this.Shoot();
        }
    }

    // Token: 0x06000085 RID: 133 RVA: 0x00004010 File Offset: 0x00002210
    private void Shoot()
    {
        this.readyToShoot = false;
        this.cylinderAnimator.SetTrigger("hasShot");
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit raycastHit;
        Vector3 point;
        if (Physics.Raycast(ray, out raycastHit))
        {
            point = raycastHit.point;
        }
        else
        {
            point = ray.GetPoint(75f);
        }
        Vector3 a = point - this.attackPoint.position;
        float x = Random.Range(-this.spread, this.spread);
        float y = Random.Range(-this.spread, this.spread);
        Vector3 vector = a + new Vector3(x, y, 0f);
        GameObject gameObject = Object.Instantiate<GameObject>(this.bullet, this.attackPoint.position, Quaternion.identity);
        gameObject.transform.forward = vector.normalized;
        gameObject.GetComponent<Rigidbody>().AddForce(vector.normalized * this.shootForce, ForceMode.Impulse);
        gameObject.GetComponent<Rigidbody>().AddForce(Camera.main.transform.up * this.upwardForce, ForceMode.Impulse);
        if (this.muzzleFlash != null)
        {
            Object.Instantiate<GameObject>(this.muzzleFlash, this.attackPoint.position, Quaternion.identity).transform.parent = this.attackPoint;
        }
        this.bulletsLeft--;
        this.bulletsShot++;
        this.UpdateText();
        if (this.allowInvoke)
        {
            base.Invoke("ResetShot", this.timeBetweenShooting);
            this.allowInvoke = false;
            this.playerRb.AddForce(-vector.normalized * this.recoilForce, ForceMode.Impulse);
        }
        if (this.bulletsShot < this.bulletsPerTap && this.bulletsLeft > 0)
        {
            base.Invoke("Shoot", this.timeBetweenShots);
        }
    }

    // Token: 0x06000086 RID: 134 RVA: 0x000041F6 File Offset: 0x000023F6
    private void ResetShot()
    {
        this.readyToShoot = true;
        this.allowInvoke = true;
    }

    // Token: 0x06000087 RID: 135 RVA: 0x00004208 File Offset: 0x00002408
    private void Reload()
    {
        this._animator.SetTrigger("startReloading");
        this.reloading = true;
        this.ammunitionDisplay.enabled = false;
        this.ammunitionDisplay.transform.GetChild(0).gameObject.SetActive(true);
        base.Invoke("ReloadFinished", this.reloadTime);
    }

    // Token: 0x06000088 RID: 136 RVA: 0x00004268 File Offset: 0x00002468
    private void ReloadFinished()
    {
        this.ammunitionDisplay.enabled = true;
        this.ammunitionDisplay.transform.GetChild(0).gameObject.SetActive(false);
        this.bulletsLeft = this.magazineSize;
        this.reloading = false;
        this.UpdateText();
    }

    // Token: 0x06000089 RID: 137 RVA: 0x000042B8 File Offset: 0x000024B8
    private void UpdateText()
    {
        if (this.ammunitionDisplay != null)
        {
            this.ammunitionDisplay.SetText((this.bulletsLeft / this.bulletsPerTap).ToString() + " / " + (this.magazineSize / this.bulletsPerTap).ToString(), true);
        }
    }

    // Token: 0x0600008A RID: 138 RVA: 0x00004313 File Offset: 0x00002513
    public void TriggerCylinderReload()
    {
        this.cylinderAnimator.SetTrigger("fullReload");
    }

    // Token: 0x040000AD RID: 173
    public GameObject bullet;

    // Token: 0x040000AE RID: 174
    public float shootForce;

    // Token: 0x040000AF RID: 175
    public float upwardForce;

    // Token: 0x040000B0 RID: 176
    public float timeBetweenShooting;

    // Token: 0x040000B1 RID: 177
    public float spread;

    // Token: 0x040000B2 RID: 178
    public float reloadTime;

    // Token: 0x040000B3 RID: 179
    public float timeBetweenShots;

    // Token: 0x040000B4 RID: 180
    public int magazineSize;

    // Token: 0x040000B5 RID: 181
    public int bulletsPerTap;

    // Token: 0x040000B6 RID: 182
    public bool allowButtonHold;

    // Token: 0x040000B7 RID: 183
    private int bulletsLeft;

    // Token: 0x040000B8 RID: 184
    private int bulletsShot;

    // Token: 0x040000B9 RID: 185
    public Rigidbody playerRb;

    // Token: 0x040000BA RID: 186
    public float recoilForce;

    // Token: 0x040000BB RID: 187
    private bool shooting;

    // Token: 0x040000BC RID: 188
    private bool readyToShoot;

    // Token: 0x040000BD RID: 189
    private bool reloading;

    // Token: 0x040000BE RID: 190
    public Transform attackPoint;

    // Token: 0x040000BF RID: 191
    public GameObject muzzleFlash;

    // Token: 0x040000C0 RID: 192
    public GameObject gunText;

    // Token: 0x040000C1 RID: 193
    public TextMeshProUGUI ammunitionDisplay;

    // Token: 0x040000C2 RID: 194
    public Animator cylinderAnimator;

    // Token: 0x040000C3 RID: 195
    public bool allowInvoke = true;

    // Token: 0x040000C4 RID: 196
    private Animator _animator;
}

using System;
using UnityEngine;

// Token: 0x02000014 RID: 20
public class RunOver : MonoBehaviour
{
    // Token: 0x06000076 RID: 118 RVA: 0x00003D9E File Offset: 0x00001F9E
    private void Start()
    {
        this.gateAnimator.SetBool("hasMushrooms", true);
    }

    // Token: 0x06000077 RID: 119 RVA: 0x00003DB1 File Offset: 0x00001FB1
    private void Update()
    {
        this.hasFinishedRun = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        if (this.hasFinishedRun)
        {
            this.EndRun();
        }
    }

    // Token: 0x06000078 RID: 120 RVA: 0x00003DE8 File Offset: 0x00001FE8
    public void EndRun()
    {
        this.enemyRun.TakeDamage(50);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.FindObjectOfType<PlayerController>().moveSpeed = 3.5f;
        this.gateAnimator.SetBool("pastGate", true);
        base.enabled = false;
    }

    // Token: 0x06000079 RID: 121 RVA: 0x00003E34 File Offset: 0x00002034
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x040000A0 RID: 160
    public Animator gateAnimator;

    // Token: 0x040000A1 RID: 161
    public Enemy enemyRun;

    // Token: 0x040000A2 RID: 162
    public LayerMask whatIsPlayer;

    // Token: 0x040000A3 RID: 163
    public float sightRange;

    // Token: 0x040000A4 RID: 164
    private bool hasFinishedRun;
}

using System;
using System.Collections;
using UnityEngine;

// Token: 0x02000067 RID: 103
public class Sable : MonoBehaviour
{
    // Token: 0x060001F6 RID: 502 RVA: 0x00009DE8 File Offset: 0x00007FE8
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            int num = Random.Range(1, 5);
            int num2 = Random.Range(0, this.swingSounds.Length);
            this.weaponAnimator.Play(num.ToString());
            Object.Instantiate<GameObject>(this.swingSounds[num2], base.transform.position, Quaternion.identity);
            this.clickChecker = this.ClickCheck();
            base.StartCoroutine(this.clickChecker);
        }
    }

    // Token: 0x060001F7 RID: 503 RVA: 0x00009E60 File Offset: 0x00008060
    private void OnTriggerEnter(Collider other)
    {
        if (this.clickChecker == null)
        {
            return;
        }
        Enemy enemy;
        PatapimBoss patapimBoss;
        if (other.transform.root.TryGetComponent<Enemy>(out enemy))
        {
            enemy.TakeDamage(this.damage);
        }
        else if (other.transform.root.TryGetComponent<PatapimBoss>(out patapimBoss))
        {
            patapimBoss.TakeDamage(this.damage);
        }
        else
        {
            EnemyFast enemyFast;
            if (!other.transform.root.TryGetComponent<EnemyFast>(out enemyFast))
            {
                return;
            }
            enemyFast.TakeDamage(this.damage);
        }
        Debug.Log("HIT");
        this.InstantiateSounds(other);
        base.StopCoroutine(this.clickChecker);
        this.clickChecker = null;
    }

    // Token: 0x060001F8 RID: 504 RVA: 0x00009F04 File Offset: 0x00008104
    private void InstantiateSounds(Collider other)
    {
        int num = Random.Range(0, this.damageSounds.Length);
        Object.Instantiate<GameObject>(this.damageSounds[num], base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.damageEffect, other.ClosestPointOnBounds(base.transform.position), Quaternion.identity);
    }

    // Token: 0x060001F9 RID: 505 RVA: 0x00009F60 File Offset: 0x00008160
    private IEnumerator ClickCheck()
    {
        yield return new WaitForSeconds(0.2f);
        this.clickChecker = null;
        yield break;
    }

    // Token: 0x0400027C RID: 636
    public int damage;

    // Token: 0x0400027D RID: 637
    public GameObject[] damageSounds;

    // Token: 0x0400027E RID: 638
    public Animator weaponAnimator;

    // Token: 0x0400027F RID: 639
    public GameObject[] swingSounds;

    // Token: 0x04000280 RID: 640
    public GameObject damageEffect;

    // Token: 0x04000281 RID: 641
    private Vector3 sparksDirection;

    // Token: 0x04000282 RID: 642
    private IEnumerator clickChecker;
}

using System;
using UnityEngine;
using UnityEngine.Events;

// Token: 0x02000075 RID: 117
public class SahurDialogueSwitch : MonoBehaviour
{
    // Token: 0x0600025D RID: 605 RVA: 0x0000BC6C File Offset: 0x00009E6C
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (this.firstTime)
            {
                this.dialogues[0].Invoke();
                this.firstTime = false;
                return;
            }
            if (this.collector.items < 30)
            {
                this.dialogues[1].Invoke();
                return;
            }
            if (this.collector.items >= 30)
            {
                if (!this.HasCompletedGame)
                {
                    this.dialogues[2].Invoke();
                    this.gameManager.HasSeenDialogue = true;
                    return;
                }
                this.fenceAnimator.SetBool("hasMushrooms", true);
                this.dialogues[3].Invoke();
            }
        }
    }

    // Token: 0x0600025E RID: 606 RVA: 0x0000BD12 File Offset: 0x00009F12
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.skip.Invoke();
        }
    }

    // Token: 0x04000311 RID: 785
    public bool firstTime = true;

    // Token: 0x04000312 RID: 786
    public ItemCollector collector;

    // Token: 0x04000313 RID: 787
    public UnityEvent[] dialogues;

    // Token: 0x04000314 RID: 788
    public UnityEvent skip;

    // Token: 0x04000315 RID: 789
    public Animator fenceAnimator;

    // Token: 0x04000316 RID: 790
    public SahurGameManager gameManager;

    // Token: 0x04000317 RID: 791
    public bool HasCompletedGame;
}

using System;
using System.Collections;
using Cinemachine;
using UnityEngine;

// Token: 0x02000076 RID: 118
public class SahurGameManager : MonoBehaviour
{
    // Token: 0x06000260 RID: 608 RVA: 0x0000BD3C File Offset: 0x00009F3C
    private void Update()
    {
        if (this.isEnabled && Input.GetKeyDown(KeyCode.E))
        {
            if (this.ruleText1.activeInHierarchy)
            {
                this.ruleText1.SetActive(false);
                this.ruleText2.SetActive(true);
                return;
            }
            this.ruleText1.SetActive(true);
            this.ruleText2.SetActive(false);
        }
    }

    // Token: 0x06000261 RID: 609 RVA: 0x0000BD98 File Offset: 0x00009F98
    public void EnableGame()
    {
        if (this.HasSeenDialogue && !this.sahurDialogueSwitch.HasCompletedGame && !this.isEnabled)
        {
            this.isEnabled = true;
            this.cam.Priority = 20;
            this.gameUI.SetActive(true);
            this.gameCharacters.SetActive(true);
            this.player.SetActive(false);
            GameObject[] array = this.thingsInHand;
            for (int i = 0; i < array.Length; i++)
            {
                array[i].SetActive(false);
            }
            this.sahurGuard.SetActive(false);
            base.StartCoroutine(this.ShowRules());
        }
    }

    // Token: 0x06000262 RID: 610 RVA: 0x0000BE34 File Offset: 0x0000A034
    public IEnumerator ShowRules()
    {
        yield return new WaitForSeconds(0.1f);
        Debug.Log("HEY");
        this.ruleText1.SetActive(false);
        this.ruleText2.SetActive(true);
        yield break;
    }

    // Token: 0x06000263 RID: 611 RVA: 0x0000BE44 File Offset: 0x0000A044
    public void DisableGame()
    {
        this.isEnabled = false;
        this.cam.Priority = 0;
        this.gameUI.SetActive(false);
        this.gameCharacters.SetActive(false);
        this.player.SetActive(true);
        this.thingsInHand[0].SetActive(true);
        this.thingsInHand[2].SetActive(true);
        this.sahurGuard.SetActive(true);
        this.sahurDialogueSwitch.HasCompletedGame = true;
    }

    // Token: 0x04000318 RID: 792
    public CinemachineVirtualCamera cam;

    // Token: 0x04000319 RID: 793
    public GameObject gameUI;

    // Token: 0x0400031A RID: 794
    public GameObject gameCharacters;

    // Token: 0x0400031B RID: 795
    public bool HasSeenDialogue;

    // Token: 0x0400031C RID: 796
    public GameObject player;

    // Token: 0x0400031D RID: 797
    public GameObject[] thingsInHand;

    // Token: 0x0400031E RID: 798
    public GameObject sahurGuard;

    // Token: 0x0400031F RID: 799
    public SahurDialogueSwitch sahurDialogueSwitch;

    // Token: 0x04000320 RID: 800
    public GameObject ruleText1;

    // Token: 0x04000321 RID: 801
    public GameObject ruleText2;

    // Token: 0x04000322 RID: 802
    private bool isEnabled;
}

using System;
using UnityEngine;

// Token: 0x02000077 RID: 119
public class SahurHitChecker : MonoBehaviour
{
    // Token: 0x06000265 RID: 613 RVA: 0x0000BEC4 File Offset: 0x0000A0C4
    public void Check()
    {
        MoleBotController.Instance.CheckHit();
        Debug.Log("Hit!");
    }
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x02000049 RID: 73
public class SaveDropdownValue : MonoBehaviour
{
    // Token: 0x0600017E RID: 382 RVA: 0x00007F25 File Offset: 0x00006125
    private void Awake()
    {
        this.dropdown = base.GetComponent<TMP_Dropdown>();
        this.dropdown.onValueChanged.AddListener(delegate (int index)
        {
            PlayerPrefs.SetInt("optionvalue", this.dropdown.value);
            PlayerPrefs.Save();
        });
    }

    // Token: 0x0600017F RID: 383 RVA: 0x00007F4F File Offset: 0x0000614F
    private void Start()
    {
        this.dropdown.value = PlayerPrefs.GetInt("optionvalue", 2);
    }

    // Token: 0x04000205 RID: 517
    private const string PrefName = "optionvalue";

    // Token: 0x04000206 RID: 518
    private TMP_Dropdown dropdown;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Token: 0x0200003E RID: 62
public class SceneLoader : MonoBehaviour
{
    // Token: 0x0600014F RID: 335 RVA: 0x0000785D File Offset: 0x00005A5D
    private void OnEnable()
    {
        this.LoadScene(this.sceneIndex);
    }

    // Token: 0x06000150 RID: 336 RVA: 0x0000786B File Offset: 0x00005A6B
    public void LoadScene(int index)
    {
        if (index == 2)
        {
            PlayerPrefs.SetInt("Scene", 2);
        }
        base.StartCoroutine(this.LoadAsync(index));
    }

    // Token: 0x06000151 RID: 337 RVA: 0x0000788A File Offset: 0x00005A8A
    public IEnumerator LoadAsync(int index)
    {
        this.progressSlider.value = 0f;
        this.LoaderUI.SetActive(true);
        AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(index);
        asyncOperation.allowSceneActivation = false;
        float progress = 0f;
        while (!asyncOperation.isDone)
        {
            progress = Mathf.MoveTowards(progress, asyncOperation.progress, Time.deltaTime);
            this.progressSlider.value = progress;
            if (progress >= 0.9f)
            {
                this.progressSlider.value = 1f;
                asyncOperation.allowSceneActivation = true;
            }
            yield return null;
        }
        yield break;
    }

    // Token: 0x040001DF RID: 479
    public int sceneIndex;

    // Token: 0x040001E0 RID: 480
    public GameObject LoaderUI;

    // Token: 0x040001E1 RID: 481
    public Slider progressSlider;
}

using System;
using UnityEngine;

// Token: 0x0200005C RID: 92
public class SceneSaver : MonoBehaviour
{
    // Token: 0x060001C9 RID: 457 RVA: 0x00008CBE File Offset: 0x00006EBE
    private void Start()
    {
    }

    // Token: 0x060001CA RID: 458 RVA: 0x00008CC0 File Offset: 0x00006EC0
    private void Update()
    {
    }
}

using System;
using UnityEngine;

// Token: 0x0200005D RID: 93
public class SecretFox : MonoBehaviour
{
    // Token: 0x060001CC RID: 460 RVA: 0x00008CCA File Offset: 0x00006ECA
    private void OnDestroy()
    {
        PlayerPrefs.SetInt("Magazine", 16);
        this.gun.magazineSize = 16;
    }

    // Token: 0x0400024D RID: 589
    [SerializeField]
    private ProjectileGun gun;
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x0200004A RID: 74
public class SetQuality : MonoBehaviour
{
    // Token: 0x06000182 RID: 386 RVA: 0x00007F8B File Offset: 0x0000618B
    public void SetQualityLevel(int index)
    {
        QualitySettings.SetQualityLevel(index, false);
        this.settings.SaveQuality(index);
    }

    // Token: 0x04000207 RID: 519
    [SerializeField]
    private TMP_Dropdown qualityDropdown;

    // Token: 0x04000208 RID: 520
    [SerializeField]
    private GameSettings settings;
}

using System;
using UnityEngine;

// Token: 0x02000028 RID: 40
public class ShaiActivator : MonoBehaviour
{
    // Token: 0x060000E7 RID: 231 RVA: 0x0000583B File Offset: 0x00003A3B
    private void Start()
    {
        this.music = Object.FindObjectOfType<MusicManager>();
    }

    // Token: 0x060000E8 RID: 232 RVA: 0x00005848 File Offset: 0x00003A48
    public void OnPick()
    {
        this.shaiBoss.SetActive(true);
        this.gate.SetActive(true);
        if (!(this.music.audioSource.clip != this.music.musicVariants[5]) && !(this.music.audioSource.clip != this.music.musicVariants[6]))
        {
            this.music.EnableMusic(0);
            return;
        }
        if (Random.Range(0, 10) == 9)
        {
            Debug.Log("POVEZLOOO");
            this.music.EnableMusic(6);
            return;
        }
        this.music.EnableMusic(5);
    }

    // Token: 0x0400013C RID: 316
    public GameObject shaiBoss;

    // Token: 0x0400013D RID: 317
    public GameObject gate;

    // Token: 0x0400013E RID: 318
    private MusicManager music;
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Token: 0x0200000D RID: 13
public class Shailushay : MonoBehaviour
{
    // Token: 0x06000053 RID: 83 RVA: 0x000035EA File Offset: 0x000017EA
    private void Awake()
    {
        this.agent = base.GetComponent<NavMeshAgent>();
        this.animator = base.GetComponentInChildren<Animator>();
        this.health = this.startHealth;
    }

    // Token: 0x06000054 RID: 84 RVA: 0x00003610 File Offset: 0x00001810
    private void Start()
    {
        this.healthSlider.gameObject.SetActive(true);
        this.damage = this.startDamage;
    }

    // Token: 0x06000055 RID: 85 RVA: 0x00003630 File Offset: 0x00001830
    private void Update()
    {
        this.healthSlider.value = this.health / this.startHealth;
        this.playerInSightRange = Physics.CheckSphere(base.transform.position, this.sightRange, this.whatIsPlayer);
        this.playerInAttackRange = Physics.CheckSphere(base.transform.position, this.attackRange, this.whatIsPlayer);
        if (!this.playerInSightRange && !this.playerInAttackRange)
        {
            this.Patroling();
        }
        if (this.playerInSightRange && !this.playerInAttackRange)
        {
            this.ChasePlayer();
        }
        if (this.playerInAttackRange && this.playerInSightRange)
        {
            this.AttackPlayer();
        }
    }

    // Token: 0x06000056 RID: 86 RVA: 0x000036E8 File Offset: 0x000018E8
    private void Patroling()
    {
        if (!this.walkPointSet)
        {
            this.SearchWalkPoint();
        }
        if (this.walkPointSet)
        {
            this.agent.SetDestination(this.walkPoint);
        }
        if ((base.transform.position - this.walkPoint).magnitude < 1f)
        {
            this.walkPointSet = false;
        }
    }

    // Token: 0x06000057 RID: 87 RVA: 0x0000374C File Offset: 0x0000194C
    private void SearchWalkPoint()
    {
        float num = Random.Range(-this.walkPointRange, this.walkPointRange);
        float num2 = Random.Range(-this.walkPointRange, this.walkPointRange);
        this.walkPoint = new Vector3(base.transform.position.x + num2, base.transform.position.y, base.transform.position.z + num);
        if (Physics.Raycast(this.walkPoint, -base.transform.up, 2f, this.whatIsGround))
        {
            this.walkPointSet = true;
        }
    }

    // Token: 0x06000058 RID: 88 RVA: 0x000037F2 File Offset: 0x000019F2
    private void ChasePlayer()
    {
        this.agent.SetDestination(this.player.position);
        if (!this.isAttacking)
        {
            this.isAttacking = true;
            base.StartCoroutine(this.Rest());
        }
    }

    // Token: 0x06000059 RID: 89 RVA: 0x00003828 File Offset: 0x00001A28
    private void AttackPlayer()
    {
        this.agent.SetDestination(base.transform.position);
        base.transform.LookAt(new Vector3(this.player.position.x, base.transform.position.y, this.player.position.z));
        if (!this.alreadyAttacked)
        {
            this.player.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            this.alreadyAttacked = true;
            base.Invoke("ResetAttack", this.timeBetweenAttacks);
        }
    }

    // Token: 0x0600005A RID: 90 RVA: 0x000038C4 File Offset: 0x00001AC4
    public void ChooseMoveset()
    {
        int num = Random.Range(0, 5);
        if (num == 0)
        {
            base.StartCoroutine(this.SpawnGroupSticks(5, 0.25f));
            if (this.health < 45f)
            {
                for (int i = 0; i < 7; i++)
                {
                    base.StartCoroutine(this.SummonLightning());
                }
                return;
            }
        }
        else if (num == 1)
        {
            base.StartCoroutine(this.SpawnGroupSticks(8, 0.15f));
            if (this.health < 45f)
            {
                for (int j = 0; j < 7; j++)
                {
                    base.StartCoroutine(this.SummonLightning());
                }
                return;
            }
        }
        else
        {
            if (num == 2 || num == 3)
            {
                base.StartCoroutine(this.SpeedUp());
                return;
            }
            if (num == 4)
            {
                base.StartCoroutine(this.SummonMushrooms());
            }
        }
    }

    // Token: 0x0600005B RID: 91 RVA: 0x0000397B File Offset: 0x00001B7B
    public IEnumerator SpawnGroupSticks(int totalSticks, float cooldown)
    {
        int numberOfSticks = 0;
        while (numberOfSticks <= totalSticks)
        {
            this.SpawnStick();
            int num = numberOfSticks;
            numberOfSticks = num + 1;
            yield return new WaitForSeconds(cooldown);
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x0600005C RID: 92 RVA: 0x00003998 File Offset: 0x00001B98
    public void SpawnStick()
    {
        Object.Instantiate<GameObject>(this.stick, this.attackPos.position, this.attackPos.rotation);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
    }

    // Token: 0x0600005D RID: 93 RVA: 0x000039D8 File Offset: 0x00001BD8
    public IEnumerator SummonLightning()
    {
        Vector3 lightningPos = this.RandomPointInBox(new Vector3(15f, 0f, 15f));
        Object.Instantiate<GameObject>(this.preLightning, lightningPos, Quaternion.identity);
        yield return new WaitForSeconds(1f);
        Object.Instantiate<GameObject>(this.lightning, lightningPos, Quaternion.identity);
        Object.Instantiate<GameObject>(this.magicSound, this.attackPos.position, Quaternion.identity);
        yield break;
    }

    // Token: 0x0600005E RID: 94 RVA: 0x000039E7 File Offset: 0x00001BE7
    public IEnumerator SummonMushrooms()
    {
        if (this.health > 45f)
        {
            this.easySpawner.enabled = true;
            yield return new WaitForSeconds(2f);
            this.easySpawner.enabled = false;
        }
        else
        {
            this.hardSpawner.enabled = true;
            yield return new WaitForSeconds(3f);
            this.hardSpawner.enabled = false;
        }
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x0600005F RID: 95 RVA: 0x000039F6 File Offset: 0x00001BF6
    public IEnumerator Rest()
    {
        this.restCount++;
        if (this.restCount > 2)
        {
            this.animator.SetBool("isStanding", true);
            this.agent.speed = 0f;
            this.damage = 0;
            yield return new WaitForSeconds(4.5f);
            this.restCount = 0;
            this.animator.SetBool("isStanding", false);
            this.damage = this.startDamage;
            this.agent.speed = this.startSpeed;
        }
        this.ChooseMoveset();
        yield break;
    }

    // Token: 0x06000060 RID: 96 RVA: 0x00003A05 File Offset: 0x00001C05
    public IEnumerator SpeedUp()
    {
        this.restCount += 3;
        this.agent.speed = this.startSpeed * 2.4f;
        this.animator.SetFloat("runSpeed", 2f);
        yield return new WaitForSeconds(7f);
        this.agent.speed = this.startSpeed;
        this.animator.SetFloat("runSpeed", 1f);
        this.isAttacking = false;
        yield break;
    }

    // Token: 0x06000061 RID: 97 RVA: 0x00003A14 File Offset: 0x00001C14
    private Vector3 RandomPointInBox(Vector3 size)
    {
        return new Vector3(this.player.position.x, 19f, this.player.position.z) + new Vector3((Random.value - 0.5f) * size.x, (Random.value - 0.5f) * size.y, (Random.value - 0.5f) * size.z);
    }

    // Token: 0x06000062 RID: 98 RVA: 0x00003A8B File Offset: 0x00001C8B
    private void ResetAttack()
    {
        this.alreadyAttacked = false;
    }

    // Token: 0x06000063 RID: 99 RVA: 0x00003A94 File Offset: 0x00001C94
    public void TakeDamage(int damage)
    {
        this.health -= (float)damage;
        if (this.health <= 0f)
        {
            base.Invoke("DestroyEnemy", 0.5f);
        }
    }

    // Token: 0x06000064 RID: 100 RVA: 0x00003AC4 File Offset: 0x00001CC4
    private void DestroyEnemy()
    {
        Object.Instantiate<GameObject>(this.deathEffect, base.transform.position, Quaternion.identity);
        Object.Instantiate<GameObject>(this.finalMushroom, base.transform.position, Quaternion.identity);
        Object.FindObjectOfType<MusicManager>().EnableMusic(0);
        Object.Destroy(base.gameObject);
    }

    // Token: 0x06000065 RID: 101 RVA: 0x00003B20 File Offset: 0x00001D20
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(base.transform.position, this.attackRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(base.transform.position, this.sightRange);
    }

    // Token: 0x04000071 RID: 113
    public LayerMask whatIsGround;

    // Token: 0x04000072 RID: 114
    public LayerMask whatIsPlayer;

    // Token: 0x04000073 RID: 115
    public Vector3 walkPoint;

    // Token: 0x04000074 RID: 116
    private bool walkPointSet;

    // Token: 0x04000075 RID: 117
    public float walkPointRange;

    // Token: 0x04000076 RID: 118
    public float timeBetweenAttacks;

    // Token: 0x04000077 RID: 119
    private bool isAttacking;

    // Token: 0x04000078 RID: 120
    private bool alreadyAttacked;

    // Token: 0x04000079 RID: 121
    public float sightRange;

    // Token: 0x0400007A RID: 122
    public float attackRange;

    // Token: 0x0400007B RID: 123
    public bool playerInSightRange;

    // Token: 0x0400007C RID: 124
    public bool playerInAttackRange;

    // Token: 0x0400007D RID: 125
    public float startHealth;

    // Token: 0x0400007E RID: 126
    public float health;

    // Token: 0x0400007F RID: 127
    public int startDamage;

    // Token: 0x04000080 RID: 128
    public float startSpeed;

    // Token: 0x04000081 RID: 129
    private int damage;

    // Token: 0x04000082 RID: 130
    private int restCount;

    // Token: 0x04000083 RID: 131
    public GameObject stick;

    // Token: 0x04000084 RID: 132
    public GameObject preLightning;

    // Token: 0x04000085 RID: 133
    public GameObject lightning;

    // Token: 0x04000086 RID: 134
    public Transform attackPos;

    // Token: 0x04000087 RID: 135
    public Transform lightningCenter;

    // Token: 0x04000088 RID: 136
    public GameObject magicSound;

    // Token: 0x04000089 RID: 137
    public GameObject deathEffect;

    // Token: 0x0400008A RID: 138
    public GameObject finalMushroom;

    // Token: 0x0400008B RID: 139
    public Slider healthSlider;

    // Token: 0x0400008C RID: 140
    private NavMeshAgent agent;

    // Token: 0x0400008D RID: 141
    public Transform player;

    // Token: 0x0400008E RID: 142
    private Animator animator;

    // Token: 0x0400008F RID: 143
    private GameObject newStick;

    // Token: 0x04000090 RID: 144
    private CurveSpawner easySpawner;

    // Token: 0x04000091 RID: 145
    private CurveSpawner hardSpawner;

    // Token: 0x04000092 RID: 146
    private Vector3 stickDirection;
}

using System;
using UnityEngine;

// Token: 0x02000078 RID: 120
public class Shark : MonoBehaviour
{
    // Token: 0x06000267 RID: 615 RVA: 0x0000BEE2 File Offset: 0x0000A0E2
    public void Freeze(bool value)
    {
        this.frozen = value;
    }

    // Token: 0x06000268 RID: 616 RVA: 0x0000BEEC File Offset: 0x0000A0EC
    private void Update()
    {
        if (this.frozen || !base.gameObject.activeSelf)
        {
            return;
        }
        if (this.state == Shark.State.Rising)
        {
            float num = Mathf.MoveTowards(base.transform.position.y, this.waterY, this.riseSpeed * Time.deltaTime);
            base.transform.position = new Vector3(base.transform.position.x, num, base.transform.position.z);
            if (num >= this.waterY)
            {
                this.state = Shark.State.Moving;
                return;
            }
        }
        else if (this.state == Shark.State.Moving)
        {
            base.transform.position += this.direction * this.speed * Time.deltaTime;
            base.transform.position = new Vector3(base.transform.position.x, this.waterY, base.transform.position.z);
            if (this.ReachedEdge())
            {
                this.state = Shark.State.Diving;
                return;
            }
        }
        else if (this.state == Shark.State.Diving)
        {
            base.transform.position += Vector3.down * this.diveSpeed * Time.deltaTime;
            if (base.transform.position.y <= this.waterY + this.diveDepth)
            {
                base.gameObject.SetActive(false);
            }
        }
    }

    // Token: 0x06000269 RID: 617 RVA: 0x0000C068 File Offset: 0x0000A268
    public void Activate(bool horizontalLane, float x, float z, Vector2 min, Vector2 max, bool fromMinSide)
    {
        this.horizontal = horizontalLane;
        this.waterMin = min;
        this.waterMax = max;
        this.state = Shark.State.Rising;
        Vector3 position;
        if (this.horizontal)
        {
            position = new Vector3(fromMinSide ? this.waterMin.x : this.waterMax.x, this.waterY + this.startBelow, z);
            this.direction = (fromMinSide ? Vector3.right : Vector3.left);
        }
        else
        {
            position = new Vector3(x, this.waterY + this.startBelow, fromMinSide ? this.waterMin.y : this.waterMax.y);
            this.direction = (fromMinSide ? Vector3.forward : Vector3.back);
        }
        base.transform.position = position;
        base.transform.rotation = Quaternion.LookRotation(this.direction, Vector3.up);
        base.gameObject.SetActive(true);
    }

    // Token: 0x0600026A RID: 618 RVA: 0x0000C160 File Offset: 0x0000A360
    private bool ReachedEdge()
    {
        if (this.horizontal)
        {
            return base.transform.position.x < this.waterMin.x || base.transform.position.x > this.waterMax.x;
        }
        return base.transform.position.z < this.waterMin.y || base.transform.position.z > this.waterMax.y;
    }

    // Token: 0x04000323 RID: 803
    public float speed = 10f;

    // Token: 0x04000324 RID: 804
    public float riseSpeed = 6f;

    // Token: 0x04000325 RID: 805
    public float diveSpeed = 6f;

    // Token: 0x04000326 RID: 806
    public float waterY = -4f;

    // Token: 0x04000327 RID: 807
    public float startBelow = -4f;

    // Token: 0x04000328 RID: 808
    public float diveDepth = -5f;

    // Token: 0x04000329 RID: 809
    private bool horizontal;

    // Token: 0x0400032A RID: 810
    private Vector3 direction;

    // Token: 0x0400032B RID: 811
    private Vector2 waterMin;

    // Token: 0x0400032C RID: 812
    private Vector2 waterMax;

    // Token: 0x0400032D RID: 813
    public Transform introLookTarget;

    // Token: 0x0400032E RID: 814
    private bool frozen;

    // Token: 0x0400032F RID: 815
    private Shark.State state;

    // Token: 0x020000FC RID: 252
    private enum State
    {
        // Token: 0x040005C8 RID: 1480
        Rising,
        // Token: 0x040005C9 RID: 1481
        Moving,
        // Token: 0x040005CA RID: 1482
        Diving
    }
}

using System;
using System.Collections;
using Cinemachine;
using TMPro;
using UnityEngine;

// Token: 0x02000079 RID: 121
public class SharkManager : MonoBehaviour
{
    // Token: 0x0600026C RID: 620 RVA: 0x0000C248 File Offset: 0x0000A448
    private void Start()
    {
        this.sharkA = Object.Instantiate<Shark>(this.sharkPrefab);
        this.sharkB = Object.Instantiate<Shark>(this.sharkPrefab);
        this.plane = Object.Instantiate<PlaneAttack>(this.planePrefab);
        this.sharkA.gameObject.SetActive(false);
        this.sharkB.gameObject.SetActive(false);
        this.plane.gameObject.SetActive(false);
        this.timerText.gameObject.SetActive(false);
    }

    // Token: 0x0600026D RID: 621 RVA: 0x0000C2CC File Offset: 0x0000A4CC
    public void StartAttack()
    {
        if (this.isRunning)
        {
            return;
        }
        base.StartCoroutine(this.StageFlow());
    }

    // Token: 0x0600026E RID: 622 RVA: 0x0000C2E4 File Offset: 0x0000A4E4
    private IEnumerator StageFlow()
    {
        this.isRunning = true;
        this.fenceAnimator.SetTrigger("bossfightStarted");
        MusicManager.instance.EnableMusic(4);
        this.stage = 1;
        this.sharkLoop = base.StartCoroutine(this.SharkLoop(false));
        yield return this.RunTimer(this.stageTime, true);
        base.StopCoroutine(this.sharkLoop);
        this.stage = 2;
        this.sharkLoop = base.StartCoroutine(this.SharkLoop(true));
        yield return this.RunTimer(this.stageTime, false);
        base.StopCoroutine(this.sharkLoop);
        this.stage = 3;
        this.timerText.gameObject.SetActive(false);
        yield return new WaitForSeconds(1f);
        this.plane.gameObject.SetActive(true);
        this.plane.StartBombing(this.player, this.waterMin, this.waterMax);
        base.StartCoroutine(this.PlaneIntroCutscene());
        this.sharkLoop = base.StartCoroutine(this.SharkLoop(false));
        yield return this.RunTimer(this.stageTime, true);
        base.StopCoroutine(this.sharkLoop);
        this.stage = 4;
        this.sharkLoop = base.StartCoroutine(this.SharkLoop(true));
        yield return this.RunTimer(this.stageTime, false);
        base.StopCoroutine(this.sharkLoop);
        this.sharkA.gameObject.SetActive(false);
        this.sharkB.gameObject.SetActive(false);
        this.plane.gameObject.SetActive(false);
        this.timerText.gameObject.SetActive(false);
        this.fenceAnimator.SetTrigger("bossfightFinished");
        MusicManager.instance.EnableMusic(0);
        if (this.ladder != null)
        {
            this.ladder.SetActive(true);
        }
        this.ladderCollider.enabled = true;
        this.isRunning = false;
        yield break;
    }

    // Token: 0x0600026F RID: 623 RVA: 0x0000C2F3 File Offset: 0x0000A4F3
    private IEnumerator RunTimer(float duration, bool firstMinute)
    {
        float elapsed = 0f;
        this.timerText.gameObject.SetActive(true);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float num = Mathf.Max(firstMinute ? (120f - elapsed) : (60f - elapsed), 0f);
            int num2 = Mathf.FloorToInt(num / 60f);
            int num3 = Mathf.FloorToInt(num % 60f);
            this.timerText.text = string.Format("{0:00}:{1:00}", num2, num3);
            yield return null;
        }
        yield break;
    }

    // Token: 0x06000270 RID: 624 RVA: 0x0000C310 File Offset: 0x0000A510
    private IEnumerator SharkLoop(bool twoSharks)
    {
        for (; ; )
        {
            yield return this.SpawnWithWarning(this.sharkA, true);
            if (twoSharks)
            {
                yield return this.SpawnWithWarning(this.sharkB, false);
            }
            yield return new WaitForSeconds(this.attackInterval);
        }
        yield break;
    }

    // Token: 0x06000271 RID: 625 RVA: 0x0000C326 File Offset: 0x0000A526
    private IEnumerator SpawnWithWarning(Shark shark, bool playerAligned)
    {
        bool horizontal = Random.value > 0.5f;
        bool fromMinSide = Random.value > 0.5f;
        float x = this.player.position.x;
        float z = this.player.position.z;
        if (!playerAligned)
        {
            if (horizontal)
            {
                z += Random.Range(-this.laneOffset, this.laneOffset);
            }
            else
            {
                x += Random.Range(-this.laneOffset, this.laneOffset);
            }
        }
        x = Mathf.Clamp(x, this.waterMin.x, this.waterMax.x);
        z = Mathf.Clamp(z, this.waterMin.y, this.waterMax.y);
        Vector3 position;
        if (horizontal)
        {
            float x2 = fromMinSide ? this.waterMin.x : this.waterMax.x;
            position = new Vector3(x2, -1.8f, z);
        }
        else
        {
            float z2 = fromMinSide ? this.waterMin.y : this.waterMax.y;
            position = new Vector3(x, -1.8f, z2);
        }
        GameObject warning = Object.Instantiate<GameObject>(this.warningPrefab, position, Quaternion.identity);
        warning.transform.LookAt(new Vector3(this.player.position.x, warning.transform.position.y, this.player.position.z));
        yield return new WaitForSeconds(this.warningDelay);
        Object.Destroy(warning);
        shark.waterY = this.waterY;
        shark.Activate(horizontal, x, z, this.waterMin, this.waterMax, fromMinSide);
        if (!this.firstSharkShown)
        {
            this.firstSharkShown = true;
            base.StartCoroutine(this.DelayedSharkIntro(shark));
        }
        yield break;
    }

    // Token: 0x06000272 RID: 626 RVA: 0x0000C343 File Offset: 0x0000A543
    private IEnumerator DelayedSharkIntro(Shark shark)
    {
        while (shark.transform.position.y < this.waterY - 0.01f)
        {
            yield return null;
        }
        shark.Freeze(true);
        this.sharkIntroCam.Follow = shark.introLookTarget;
        this.sharkIntroCam.LookAt = shark.introLookTarget;
        this.sharkIntroCam.Priority = 20;
        yield return new WaitForSeconds(this.introDuration);
        this.sharkIntroCam.Priority = 0;
        shark.Freeze(false);
        yield break;
    }

    // Token: 0x06000273 RID: 627 RVA: 0x0000C359 File Offset: 0x0000A559
    private IEnumerator PlaneIntroCutscene()
    {
        this.plane.EnableBombing(false);
        this.planeIntroCam.Follow = this.plane.lookTarget;
        this.planeIntroCam.LookAt = this.plane.lookTarget;
        this.planeIntroCam.Priority = 20;
        yield return new WaitForSeconds(this.introDuration);
        this.planeIntroCam.Priority = 0;
        this.plane.EnableBombing(true);
        yield break;
    }

    // Token: 0x04000330 RID: 816
    public Shark sharkPrefab;

    // Token: 0x04000331 RID: 817
    public PlaneAttack planePrefab;

    // Token: 0x04000332 RID: 818
    public Animator fenceAnimator;

    // Token: 0x04000333 RID: 819
    public Transform player;

    // Token: 0x04000334 RID: 820
    public Vector2 waterMin;

    // Token: 0x04000335 RID: 821
    public Vector2 waterMax;

    // Token: 0x04000336 RID: 822
    public float waterY = -4f;

    // Token: 0x04000337 RID: 823
    [Header("Timing")]
    public float stageTime = 60f;

    // Token: 0x04000338 RID: 824
    public float planeStageTime = 120f;

    // Token: 0x04000339 RID: 825
    public float attackInterval = 4f;

    // Token: 0x0400033A RID: 826
    public float warningDelay = 1.5f;

    // Token: 0x0400033B RID: 827
    [Header("Lanes")]
    public float laneOffset = 4f;

    // Token: 0x0400033C RID: 828
    [Header("UI")]
    public TextMeshProUGUI timerText;

    // Token: 0x0400033D RID: 829
    [Header("Warning")]
    public GameObject warningPrefab;

    // Token: 0x0400033E RID: 830
    [Header("Cameras")]
    public CinemachineVirtualCamera sharkIntroCam;

    // Token: 0x0400033F RID: 831
    public CinemachineVirtualCamera planeIntroCam;

    // Token: 0x04000340 RID: 832
    public float introDuration = 4f;

    // Token: 0x04000341 RID: 833
    private float timeLeft;

    // Token: 0x04000342 RID: 834
    private bool isRunning;

    // Token: 0x04000343 RID: 835
    private bool firstSharkShown;

    // Token: 0x04000344 RID: 836
    private Shark sharkA;

    // Token: 0x04000345 RID: 837
    private Shark sharkB;

    // Token: 0x04000346 RID: 838
    private PlaneAttack plane;

    // Token: 0x04000347 RID: 839
    private Coroutine sharkLoop;

    // Token: 0x04000348 RID: 840
    private Transform sharkLookTarget;

    // Token: 0x04000349 RID: 841
    [Header("End Game")]
    public GameObject ladder;

    // Token: 0x0400034A RID: 842
    public Collider ladderCollider;

    // Token: 0x0400034B RID: 843
    private int stage;

    // Token: 0x020000FD RID: 253
    private enum TimerPhase
    {
        // Token: 0x040005CC RID: 1484
        FirstMinute,
        // Token: 0x040005CD RID: 1485
        SecondMinute
    }
}

using System;
using UnityEngine;

// Token: 0x02000015 RID: 21
public class ShipScreamer : MonoBehaviour
{
    // Token: 0x0600007B RID: 123 RVA: 0x00003E5E File Offset: 0x0000205E
    private void OnTriggerEnter(Collider other)
    {
        if (this.sable.activeInHierarchy)
        {
            this.screamer.SetActive(true);
        }
    }

    // Token: 0x040000A5 RID: 165
    public GameObject screamer;

    // Token: 0x040000A6 RID: 166
    public GameObject sable;
}

using System;
using UnityEngine;

// Token: 0x0200000E RID: 14
public class SmallChanceSpawner : MonoBehaviour
{
    // Token: 0x06000067 RID: 103 RVA: 0x00003B75 File Offset: 0x00001D75
    private void Start()
    {
        if (Random.Range(0, 5) == 0)
        {
            Object.Instantiate<GameObject>(this.spawnable, base.transform.position, Quaternion.identity);
        }
    }

    // Token: 0x04000093 RID: 147
    public GameObject spawnable;
}

using System;
using UnityEngine;

// Token: 0x02000042 RID: 66
public class SoundManager : MonoBehaviour
{
    // Token: 0x0600015C RID: 348 RVA: 0x000079A4 File Offset: 0x00005BA4
    private void Start()
    {
        if (this.isEnemy)
        {
            this.target = base.transform;
        }
        else
        {
            this.target = Object.FindObjectOfType<PlayerController>().transform;
        }
        if (!this.soundOnStart)
        {
            this.currentTimeBetweenSounds = this.NewSoundCooldown();
        }
    }

    // Token: 0x0600015D RID: 349 RVA: 0x000079E0 File Offset: 0x00005BE0
    public void Update()
    {
        if (this.soundEffects.Length != 0)
        {
            if (this.currentTimeBetweenSounds <= 0f)
            {
                GameObject gameObject = Object.Instantiate<GameObject>(this.soundEffects[Random.Range(0, this.soundEffects.Length)], this.target.position, Quaternion.identity);
                if (base.transform.childCount > 0)
                {
                    gameObject.transform.SetParent(base.transform.GetChild(0));
                }
                this.currentTimeBetweenSounds = this.NewSoundCooldown();
                return;
            }
            this.currentTimeBetweenSounds -= Time.deltaTime;
        }
    }

    // Token: 0x0600015E RID: 350 RVA: 0x00007A72 File Offset: 0x00005C72
    public float NewSoundCooldown()
    {
        return Random.Range(this.minSecondsBetweenSounds, this.maxSecondsBetweenSounds);
    }

    // Token: 0x040001E8 RID: 488
    public GameObject[] soundEffects;

    // Token: 0x040001E9 RID: 489
    public float minSecondsBetweenSounds;

    // Token: 0x040001EA RID: 490
    public float maxSecondsBetweenSounds;

    // Token: 0x040001EB RID: 491
    public float currentTimeBetweenSounds;

    // Token: 0x040001EC RID: 492
    public bool soundOnStart;

    // Token: 0x040001ED RID: 493
    public bool isEnemy;

    // Token: 0x040001EE RID: 494
    private Transform target;
}

using System;
using UnityEngine;

// Token: 0x0200003F RID: 63
public class SoundOnDestroy : MonoBehaviour
{
    // Token: 0x06000153 RID: 339 RVA: 0x000078A8 File Offset: 0x00005AA8
    private void OnDestroy()
    {
        Object.Instantiate<GameObject>(this.sound, base.transform.position, Quaternion.identity);
    }

    // Token: 0x040001E2 RID: 482
    public GameObject sound;
}

using System;
using UnityEngine;

// Token: 0x0200005E RID: 94
public class SpaceDoor : MonoBehaviour
{
    // Token: 0x060001CE RID: 462 RVA: 0x00008CED File Offset: 0x00006EED
    private void Start()
    {
        this._animator = base.GetComponent<Animator>();
    }

    // Token: 0x060001CF RID: 463 RVA: 0x00008CFB File Offset: 0x00006EFB
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && this.itemCollector.items >= this.itemsToEnter)
        {
            this._animator.SetBool("isOpened", true);
        }
    }

    // Token: 0x060001D0 RID: 464 RVA: 0x00008D2E File Offset: 0x00006F2E
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this._animator.SetBool("isOpened", false);
        }
    }

    // Token: 0x0400024E RID: 590
    [SerializeField]
    private ItemCollector itemCollector;

    // Token: 0x0400024F RID: 591
    [SerializeField]
    private int itemsToEnter;

    // Token: 0x04000250 RID: 592
    private Animator _animator;
}

using System;
using UnityEngine;

// Token: 0x02000068 RID: 104
public class SparksBullet : MonoBehaviour
{
    // Token: 0x060001FB RID: 507 RVA: 0x00009F78 File Offset: 0x00008178
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            if (other.transform.root.GetComponent<Enemy>() != null)
            {
                other.transform.root.GetComponent<Enemy>().TakeDamage(this.bulletDamage);
                Object.Destroy(base.gameObject);
                return;
            }
            if (other.transform.root.GetComponent<MushroomHunter>() != null)
            {
                other.transform.root.GetComponent<MushroomHunter>().TakeDamage(this.bulletDamage);
                Object.Destroy(base.gameObject);
                return;
            }
            if (other.transform.root.GetComponent<Boss>() != null)
            {
                other.transform.root.GetComponent<Boss>().TakeDamage(this.bulletDamage);
                Object.Destroy(base.gameObject);
                return;
            }
        }
        else
        {
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x060001FC RID: 508 RVA: 0x0000A05D File Offset: 0x0000825D
    private void OnDestroy()
    {
        Object.Instantiate<GameObject>(this.fireBall, base.transform.position, Quaternion.identity);
    }

    // Token: 0x04000283 RID: 643
    public int bulletDamage;

    // Token: 0x04000284 RID: 644
    public GameObject fireBall;
}

using System;
using UnityEngine;

// Token: 0x0200007A RID: 122
public class SpawnEffect : MonoBehaviour
{
    // Token: 0x06000275 RID: 629 RVA: 0x0000C3C8 File Offset: 0x0000A5C8
    private void Start()
    {
        this.shaderProperty = Shader.PropertyToID("_cutoff");
        this._renderer = base.GetComponent<Renderer>();
        this.ps = base.GetComponentInChildren<ParticleSystem>();
        this.ps.main.duration = this.spawnEffectTime;
        this.ps.Play();
    }

    // Token: 0x06000276 RID: 630 RVA: 0x0000C424 File Offset: 0x0000A624
    private void Update()
    {
        if (this.timer < this.spawnEffectTime + this.pause)
        {
            this.timer += Time.deltaTime;
        }
        else
        {
            this.ps.Play();
            this.timer = 0f;
        }
        this._renderer.material.SetFloat(this.shaderProperty, this.fadeIn.Evaluate(Mathf.InverseLerp(0f, this.spawnEffectTime, this.timer)));
    }

    // Token: 0x0400034C RID: 844
    public float spawnEffectTime = 2f;

    // Token: 0x0400034D RID: 845
    public float pause = 1f;

    // Token: 0x0400034E RID: 846
    public AnimationCurve fadeIn;

    // Token: 0x0400034F RID: 847
    private ParticleSystem ps;

    // Token: 0x04000350 RID: 848
    private float timer;

    // Token: 0x04000351 RID: 849
    private Renderer _renderer;

    // Token: 0x04000352 RID: 850
    private int shaderProperty;
}

using System;
using System.Collections.Generic;
using System.Reflection;

// Token: 0x02000088 RID: 136
public static class SubclassFinder
{
    // Token: 0x060002B3 RID: 691 RVA: 0x0000D044 File Offset: 0x0000B244
    public static List<Type> GetSubclasses<T>()
    {
        List<Type> list = new List<Type>();
        Type typeFromHandle = typeof(T);
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            foreach (Type type in assemblies[i].GetTypes())
            {
                if (type.IsSubclassOf(typeFromHandle) || type == typeFromHandle)
                {
                    list.Add(type);
                }
            }
        }
        return list;
    }
}

using System;
using TMPro;
using UnityEngine;

// Token: 0x0200005F RID: 95
public class TextChanger : MonoBehaviour
{
    // Token: 0x060001D2 RID: 466 RVA: 0x00008D56 File Offset: 0x00006F56
    public void CallNextLine()
    {
        this.textUI.text = this.textLines[this.lineNumber];
    }

    // Token: 0x04000251 RID: 593
    public TextMeshProUGUI textUI;

    // Token: 0x04000252 RID: 594
    public string[] textLines;

    // Token: 0x04000253 RID: 595
    public int lineNumber;
}

using System;
using UnityEngine;

// Token: 0x02000060 RID: 96
public class TextRotator : MonoBehaviour
{
    // Token: 0x060001D4 RID: 468 RVA: 0x00008D78 File Offset: 0x00006F78
    private void Awake()
    {
        this.cam = Camera.main;
    }

    // Token: 0x060001D5 RID: 469 RVA: 0x00008D88 File Offset: 0x00006F88
    private void LateUpdate()
    {
        if (!this.cam)
        {
            return;
        }
        Vector3 forward = base.transform.position - this.cam.transform.position;
        base.transform.rotation = Quaternion.LookRotation(forward);
    }

    // Token: 0x04000254 RID: 596
    private Camera cam;
}

using System;
using UnityEngine;

// Token: 0x0200006D RID: 109
public class TextureScroll : MonoBehaviour
{
    // Token: 0x0600020D RID: 525 RVA: 0x0000A32C File Offset: 0x0000852C
    private void Start()
    {
        this.rend = base.GetComponent<Renderer>();
    }

    // Token: 0x0600020E RID: 526 RVA: 0x0000A33C File Offset: 0x0000853C
    private void Update()
    {
        float x = Time.time * this.scrollSpeed;
        this.rend.material.SetTextureOffset("_MainTex", new Vector2(x, 0f));
    }

    // Token: 0x04000296 RID: 662
    public float scrollSpeed = 0.5f;

    // Token: 0x04000297 RID: 663
    public Renderer rend;
}

using System;
using UnityEngine;

// Token: 0x0200003D RID: 61
public class ThrowingTutorial : MonoBehaviour
{
    // Token: 0x0600014A RID: 330 RVA: 0x00007741 File Offset: 0x00005941
    private void Start()
    {
        this.readyToThrow = true;
    }

    // Token: 0x0600014B RID: 331 RVA: 0x0000774A File Offset: 0x0000594A
    private void Update()
    {
        if (this.readyToThrow && this.totalThrows > 0)
        {
            this.Throw();
        }
    }

    // Token: 0x0600014C RID: 332 RVA: 0x00007764 File Offset: 0x00005964
    private void Throw()
    {
        this.readyToThrow = false;
        Rigidbody component = Object.Instantiate<GameObject>(this.objectToThrow, this.attackPoint.position, this.attackPoint.rotation).GetComponent<Rigidbody>();
        Vector3 a = this.attackPoint.transform.forward;
        RaycastHit raycastHit;
        if (Physics.Raycast(this.attackPoint.position, this.attackPoint.forward, out raycastHit, 500f))
        {
            a = (raycastHit.point - this.attackPoint.position).normalized;
        }
        Vector3 force = a * this.throwForce + base.transform.up * this.throwUpwardForce;
        component.AddForce(force, ForceMode.Impulse);
        this.totalThrows--;
        base.Invoke("ResetThrow", this.throwCooldown);
    }

    // Token: 0x0600014D RID: 333 RVA: 0x00007841 File Offset: 0x00005A41
    private void ResetThrow()
    {
        this.readyToThrow = true;
    }

    // Token: 0x040001D7 RID: 471
    [Header("References")]
    public Transform attackPoint;

    // Token: 0x040001D8 RID: 472
    public GameObject objectToThrow;

    // Token: 0x040001D9 RID: 473
    [Header("Settings")]
    public int totalThrows;

    // Token: 0x040001DA RID: 474
    public float throwCooldown;

    // Token: 0x040001DB RID: 475
    [Header("Throwing")]
    public KeyCode throwKey = KeyCode.Mouse0;

    // Token: 0x040001DC RID: 476
    public float throwForce;

    // Token: 0x040001DD RID: 477
    public float throwUpwardForce;

    // Token: 0x040001DE RID: 478
    private bool readyToThrow;
}

using System;
using UnityEngine;
using UnityEngine.Events;

// Token: 0x02000086 RID: 134
public class TriggerArea : MonoBehaviour
{
    // Token: 0x060002B0 RID: 688 RVA: 0x0000D01F File Offset: 0x0000B21F
    private void OnTriggerEnter(Collider other)
    {
        this.OnEnter.Invoke();
    }

    // Token: 0x060002B1 RID: 689 RVA: 0x0000D02C File Offset: 0x0000B22C
    private void OnTriggerExit(Collider other)
    {
        this.OnExit.Invoke();
    }

    // Token: 0x04000380 RID: 896
    public UnityEvent OnEnter;

    // Token: 0x04000381 RID: 897
    public UnityEvent OnExit;
}

using System;
using UnityEngine;

// Token: 0x02000029 RID: 41
public class TrollBullet : MonoBehaviour
{
    // Token: 0x060000EA RID: 234 RVA: 0x000058F9 File Offset: 0x00003AF9
    private void Start()
    {
        this.direction = Vector3.forward;
        this.sable = GameObject.FindGameObjectWithTag("Sable").transform.parent.GetComponent<Animator>();
    }

    // Token: 0x060000EB RID: 235 RVA: 0x00005925 File Offset: 0x00003B25
    private void Update()
    {
        base.transform.Translate(this.direction * this.speed * Time.deltaTime);
    }

    // Token: 0x060000EC RID: 236 RVA: 0x00005950 File Offset: 0x00003B50
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.GetComponent<PlayerHealth>() != null)
        {
            other.transform.root.GetComponent<PlayerHealth>().TakeDamage((float)this.damage);
            Object.Destroy(base.gameObject);
            return;
        }
        if (other.CompareTag("Sable") && !this.sable.GetCurrentAnimatorStateInfo(0).IsName("idle"))
        {
            this.direction = -this.direction;
            return;
        }
        if (other.transform.root.name == "EnemyTroll")
        {
            other.transform.root.GetComponent<Enemy>().TakeDamage(this.damageForEnemy);
            Object.Destroy(base.gameObject);
        }
    }

    // Token: 0x060000ED RID: 237 RVA: 0x00005A19 File Offset: 0x00003C19
    private void OnDestroy()
    {
        Object.Instantiate<GameObject>(this.fireBall, base.transform.position, Quaternion.identity);
    }

    // Token: 0x0400013F RID: 319
    public int damage;

    // Token: 0x04000140 RID: 320
    public int damageForEnemy;

    // Token: 0x04000141 RID: 321
    public float speed;

    // Token: 0x04000142 RID: 322
    public GameObject fireBall;

    // Token: 0x04000143 RID: 323
    public Animator sable;

    // Token: 0x04000144 RID: 324
    private Vector3 direction;
}

using System;
using UnityEngine;

// Token: 0x02000043 RID: 67
public class TrollThrower : MonoBehaviour
{
    // Token: 0x06000160 RID: 352 RVA: 0x00007A8D File Offset: 0x00005C8D
    private void Start()
    {
        this.bulletTime = (float)this.startBulletTime;
    }

    // Token: 0x06000161 RID: 353 RVA: 0x00007A9C File Offset: 0x00005C9C
    private void Update()
    {
        this.bulletTime -= Time.deltaTime;
        if (this.bulletTime <= 0f)
        {
            this.SpawnSparks();
            this.bulletTime = (float)this.startBulletTime;
        }
    }

    // Token: 0x06000162 RID: 354 RVA: 0x00007AD0 File Offset: 0x00005CD0
    public void SpawnSparks()
    {
        this.bulletPos.LookAt(this.player);
        Object.Instantiate<GameObject>(this.bullet, this.bulletPos.position, this.bulletPos.rotation);
        Object.Instantiate<GameObject>(this.sounds[Random.Range(0, this.sounds.Length)], this.bulletPos.position, Quaternion.identity);
    }

    // Token: 0x040001EF RID: 495
    public GameObject bullet;

    // Token: 0x040001F0 RID: 496
    public Transform bulletPos;

    // Token: 0x040001F1 RID: 497
    public GameObject[] sounds;

    // Token: 0x040001F2 RID: 498
    public Transform player;

    // Token: 0x040001F3 RID: 499
    public int startBulletTime;

    // Token: 0x040001F4 RID: 500
    private float bulletTime;
}

using System;
using UnityEngine;

// Token: 0x0200002A RID: 42
public class Trotil : MonoBehaviour
{
    // Token: 0x060000EF RID: 239 RVA: 0x00005A3F File Offset: 0x00003C3F
    private void Start()
    {
        base.GetComponent<Rigidbody>().AddForce(this.startPush + base.transform.forward * 8f, ForceMode.Impulse);
    }

    // Token: 0x060000F0 RID: 240 RVA: 0x00005A6D File Offset: 0x00003C6D
    public void OnExploded()
    {
        Object.Instantiate<GameObject>(this.explosion, base.transform.position, Quaternion.identity);
        Object.Destroy(base.transform.parent.gameObject);
    }

    // Token: 0x04000145 RID: 325
    public GameObject explosion;

    // Token: 0x04000146 RID: 326
    public Vector3 startPush;
}

using System;
using UnityEngine;
using UnityEngine.Video;

// Token: 0x02000044 RID: 68
public class TVBehaviour : MonoBehaviour
{
    // Token: 0x06000164 RID: 356 RVA: 0x00007B43 File Offset: 0x00005D43
    private void Start()
    {
        this.spriteRenderer = base.GetComponent<SpriteRenderer>();
    }

    // Token: 0x06000165 RID: 357 RVA: 0x00007B51 File Offset: 0x00005D51
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.spriteRenderer.color = Color.white;
            this.clip.Play();
        }
    }

    // Token: 0x06000166 RID: 358 RVA: 0x00007B7B File Offset: 0x00005D7B
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            this.spriteRenderer.color = Color.black;
            this.clip.Stop();
        }
    }

    // Token: 0x040001F5 RID: 501
    private SpriteRenderer spriteRenderer;

    // Token: 0x040001F6 RID: 502
    public VideoPlayer clip;
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Token: 0x02000082 RID: 130
public class VariableJoystick : Joystick
{
    // Token: 0x17000011 RID: 17
    // (get) Token: 0x060002A2 RID: 674 RVA: 0x0000CBC0 File Offset: 0x0000ADC0
    // (set) Token: 0x060002A3 RID: 675 RVA: 0x0000CBC8 File Offset: 0x0000ADC8
    public float MoveThreshold
    {
        get
        {
            return this.moveThreshold;
        }
        set
        {
            this.moveThreshold = Mathf.Abs(value);
        }
    }

    // Token: 0x060002A4 RID: 676 RVA: 0x0000CBD6 File Offset: 0x0000ADD6
    public void SetMode(JoystickType joystickType)
    {
        this.joystickType = joystickType;
        if (joystickType == JoystickType.Fixed)
        {
            this.background.anchoredPosition = this.fixedPosition;
            this.background.gameObject.SetActive(true);
            return;
        }
        this.background.gameObject.SetActive(false);
    }

    // Token: 0x060002A5 RID: 677 RVA: 0x0000CC16 File Offset: 0x0000AE16
    protected override void Start()
    {
        base.Start();
        this.fixedPosition = this.background.anchoredPosition;
        this.SetMode(this.joystickType);
    }

    // Token: 0x060002A6 RID: 678 RVA: 0x0000CC3B File Offset: 0x0000AE3B
    public override void OnPointerDown(PointerEventData eventData)
    {
        if (this.joystickType != JoystickType.Fixed)
        {
            this.background.anchoredPosition = base.ScreenPointToAnchoredPosition(eventData.position);
            this.background.gameObject.SetActive(true);
        }
        base.OnPointerDown(eventData);
    }

    // Token: 0x060002A7 RID: 679 RVA: 0x0000CC74 File Offset: 0x0000AE74
    public override void OnPointerUp(PointerEventData eventData)
    {
        if (this.joystickType != JoystickType.Fixed)
        {
            this.background.gameObject.SetActive(false);
        }
        base.OnPointerUp(eventData);
    }

    // Token: 0x060002A8 RID: 680 RVA: 0x0000CC98 File Offset: 0x0000AE98
    protected override void HandleInput(float magnitude, Vector2 normalised, Vector2 radius, Camera cam)
    {
        if (this.joystickType == JoystickType.Dynamic && magnitude > this.moveThreshold)
        {
            Vector2 b = normalised * (magnitude - this.moveThreshold) * radius;
            this.background.anchoredPosition += b;
        }
        base.HandleInput(magnitude, normalised, radius, cam);
    }

    // Token: 0x0400036A RID: 874
    [SerializeField]
    private float moveThreshold = 1f;

    // Token: 0x0400036B RID: 875
    [SerializeField]
    private JoystickType joystickType;

    // Token: 0x0400036C RID: 876
    private Vector2 fixedPosition = Vector2.zero;
}

using System;
using Ditzelgames;
using UnityEngine;

// Token: 0x0200002B RID: 43
[RequireComponent(typeof(WaterFloat))]
public class WaterBoat : MonoBehaviour
{
    // Token: 0x060000F2 RID: 242 RVA: 0x00005AA8 File Offset: 0x00003CA8
    public void Awake()
    {
        this.ParticleSystem = base.GetComponentInChildren<ParticleSystem>();
        this.Rigidbody = base.GetComponent<Rigidbody>();
        this.StartRotation = this.Motor.localRotation;
        this.Camera = Camera.main;
    }

    // Token: 0x060000F3 RID: 243 RVA: 0x00005AE0 File Offset: 0x00003CE0
    public void FixedUpdate()
    {
        Vector3 forward = base.transform.forward;
        int num = 0;
        if (Input.GetKey(KeyCode.A))
        {
            num = 1;
        }
        if (Input.GetKey(KeyCode.D))
        {
            num = -1;
        }
        this.Rigidbody.AddForceAtPosition((float)num * base.transform.right * this.SteerPower / 100f, this.Motor.position);
        Vector3 a = Vector3.Scale(new Vector3(1f, 0f, 1f), base.transform.forward);
        Vector3 zero = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
        {
            PhysicsHelper.ApplyForceToReachVelocity(this.Rigidbody, a * this.MaxSpeed, this.Power, ForceMode.Force);
        }
        if (Input.GetKey(KeyCode.S))
        {
            PhysicsHelper.ApplyForceToReachVelocity(this.Rigidbody, a * -this.MaxSpeed, this.Power, ForceMode.Force);
        }
        this.Motor.SetPositionAndRotation(this.Motor.position, base.transform.rotation * this.StartRotation * Quaternion.Euler(0f, 30f * (float)num, 0f));
        if (this.ParticleSystem != null)
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S))
            {
                this.ParticleSystem.Play();
            }
            else
            {
                this.ParticleSystem.Pause();
            }
        }
        bool flag = Vector3.Cross(base.transform.forward, this.Rigidbody.velocity).y < 0f;
        this.Rigidbody.velocity = Quaternion.AngleAxis(Vector3.SignedAngle(this.Rigidbody.velocity, (flag ? 1f : 0f) * base.transform.forward, Vector3.up) * this.Drag, Vector3.up) * this.Rigidbody.velocity;
    }

    // Token: 0x04000147 RID: 327
    public Transform Motor;

    // Token: 0x04000148 RID: 328
    public float SteerPower = 500f;

    // Token: 0x04000149 RID: 329
    public float Power = 5f;

    // Token: 0x0400014A RID: 330
    public float MaxSpeed = 10f;

    // Token: 0x0400014B RID: 331
    public float Drag = 0.1f;

    // Token: 0x0400014C RID: 332
    protected Rigidbody Rigidbody;

    // Token: 0x0400014D RID: 333
    protected Quaternion StartRotation;

    // Token: 0x0400014E RID: 334
    protected ParticleSystem ParticleSystem;

    // Token: 0x0400014F RID: 335
    protected Camera Camera;

    // Token: 0x04000150 RID: 336
    protected Vector3 CamVel;
}

using System;
using Ditzelgames;
using UnityEngine;

// Token: 0x02000061 RID: 97
[RequireComponent(typeof(Rigidbody))]
public class WaterFloat : MonoBehaviour
{
    // Token: 0x17000006 RID: 6
    // (get) Token: 0x060001D7 RID: 471 RVA: 0x00008DDD File Offset: 0x00006FDD
    public Vector3 Center
    {
        get
        {
            return base.transform.position + this.centerOffset;
        }
    }

    // Token: 0x060001D8 RID: 472 RVA: 0x00008DF8 File Offset: 0x00006FF8
    private void Awake()
    {
        this.Waves = Object.FindObjectOfType<Waves>();
        this.Rigidbody = base.GetComponent<Rigidbody>();
        this.Rigidbody.useGravity = false;
        this.WaterLinePoints = new Vector3[this.FloatPoints.Length];
        for (int i = 0; i < this.FloatPoints.Length; i++)
        {
            this.WaterLinePoints[i] = this.FloatPoints[i].position;
        }
        this.centerOffset = PhysicsHelper.GetCenter(this.WaterLinePoints) - base.transform.position;
    }

    // Token: 0x060001D9 RID: 473 RVA: 0x00008E88 File Offset: 0x00007088
    private void FixedUpdate()
    {
        float num = 0f;
        bool flag = false;
        for (int i = 0; i < this.FloatPoints.Length; i++)
        {
            this.WaterLinePoints[i] = this.FloatPoints[i].position;
            this.WaterLinePoints[i].y = this.Waves.GetHeight(this.FloatPoints[i].position);
            num += this.WaterLinePoints[i].y / (float)this.FloatPoints.Length;
            if (this.WaterLinePoints[i].y > this.FloatPoints[i].position.y)
            {
                flag = true;
            }
        }
        float d = num - this.WaterLine;
        this.WaterLine = num;
        this.TargetUp = PhysicsHelper.GetNormal(this.WaterLinePoints);
        Vector3 a = Physics.gravity;
        this.Rigidbody.drag = this.AirDrag;
        if (this.WaterLine > this.Center.y)
        {
            this.Rigidbody.drag = this.WaterDrag;
            if (this.AttachToSurface)
            {
                this.Rigidbody.position = new Vector3(this.Rigidbody.position.x, this.WaterLine - this.centerOffset.y, this.Rigidbody.position.z);
            }
            else
            {
                a = (this.AffectDirection ? (this.TargetUp * -Physics.gravity.y) : (-Physics.gravity));
                base.transform.Translate(Vector3.up * d * 0.9f);
            }
        }
        this.Rigidbody.AddForce(a * Mathf.Clamp(Mathf.Abs(this.WaterLine - this.Center.y), 0f, 1f));
        if (flag)
        {
            this.TargetUp = Vector3.SmoothDamp(base.transform.up, this.TargetUp, ref this.smoothVectorRotation, 0.2f);
            this.Rigidbody.rotation = Quaternion.FromToRotation(base.transform.up, this.TargetUp) * this.Rigidbody.rotation;
        }
    }

    // Token: 0x060001DA RID: 474 RVA: 0x000090D0 File Offset: 0x000072D0
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        if (this.FloatPoints == null)
        {
            return;
        }
        for (int i = 0; i < this.FloatPoints.Length; i++)
        {
            if (!(this.FloatPoints[i] == null))
            {
                if (this.Waves != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawCube(this.WaterLinePoints[i], Vector3.one * 0.3f);
                }
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(this.FloatPoints[i].position, 0.1f);
            }
        }
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawCube(new Vector3(this.Center.x, this.WaterLine, this.Center.z), Vector3.one * 1f);
            Gizmos.DrawRay(new Vector3(this.Center.x, this.WaterLine, this.Center.z), this.TargetUp * 1f);
        }
    }

    // Token: 0x04000255 RID: 597
    public float AirDrag = 1f;

    // Token: 0x04000256 RID: 598
    public float WaterDrag = 10f;

    // Token: 0x04000257 RID: 599
    public bool AffectDirection = true;

    // Token: 0x04000258 RID: 600
    public bool AttachToSurface;

    // Token: 0x04000259 RID: 601
    public Transform[] FloatPoints;

    // Token: 0x0400025A RID: 602
    protected Rigidbody Rigidbody;

    // Token: 0x0400025B RID: 603
    protected Waves Waves;

    // Token: 0x0400025C RID: 604
    protected float WaterLine;

    // Token: 0x0400025D RID: 605
    protected Vector3[] WaterLinePoints;

    // Token: 0x0400025E RID: 606
    protected Vector3 smoothVectorRotation;

    // Token: 0x0400025F RID: 607
    protected Vector3 TargetUp;

    // Token: 0x04000260 RID: 608
    protected Vector3 centerOffset;
}

using System;
using UnityEngine;

// Token: 0x02000062 RID: 98
public class Waves : MonoBehaviour
{
    // Token: 0x060001DC RID: 476 RVA: 0x00009210 File Offset: 0x00007410
    private void Start()
    {
        this.Mesh = new Mesh();
        this.Mesh.name = base.gameObject.name;
        this.Mesh.vertices = this.GenerateVerts();
        this.Mesh.triangles = this.GenerateTries();
        this.Mesh.uv = this.GenerateUVs();
        this.Mesh.RecalculateNormals();
        this.Mesh.RecalculateBounds();
        this.MeshFilter = base.gameObject.AddComponent<MeshFilter>();
        this.MeshFilter.mesh = this.Mesh;
    }

    // Token: 0x060001DD RID: 477 RVA: 0x000092AC File Offset: 0x000074AC
    public float GetHeight(Vector3 position)
    {
        Vector3 b = new Vector3(1f / base.transform.lossyScale.x, 0f, 1f / base.transform.lossyScale.z);
        Vector3 vector = Vector3.Scale(position - base.transform.position, b);
        Vector3 vector2 = new Vector3(Mathf.Floor(vector.x), 0f, Mathf.Floor(vector.z));
        Vector3 vector3 = new Vector3(Mathf.Floor(vector.x), 0f, Mathf.Ceil(vector.z));
        Vector3 vector4 = new Vector3(Mathf.Ceil(vector.x), 0f, Mathf.Floor(vector.z));
        Vector3 vector5 = new Vector3(Mathf.Ceil(vector.x), 0f, Mathf.Ceil(vector.z));
        vector2.x = Mathf.Clamp(vector2.x, 0f, (float)this.Dimension);
        vector2.z = Mathf.Clamp(vector2.z, 0f, (float)this.Dimension);
        vector3.x = Mathf.Clamp(vector3.x, 0f, (float)this.Dimension);
        vector3.z = Mathf.Clamp(vector3.z, 0f, (float)this.Dimension);
        vector4.x = Mathf.Clamp(vector4.x, 0f, (float)this.Dimension);
        vector4.z = Mathf.Clamp(vector4.z, 0f, (float)this.Dimension);
        vector5.x = Mathf.Clamp(vector5.x, 0f, (float)this.Dimension);
        vector5.z = Mathf.Clamp(vector5.z, 0f, (float)this.Dimension);
        float num = Mathf.Max(new float[]
        {
            Vector3.Distance(vector2, vector),
            Vector3.Distance(vector3, vector),
            Vector3.Distance(vector4, vector),
            Vector3.Distance(vector5, vector) + Mathf.Epsilon
        });
        float num2 = num - Vector3.Distance(vector2, vector) + (num - Vector3.Distance(vector3, vector)) + (num - Vector3.Distance(vector4, vector)) + (num - Vector3.Distance(vector5, vector) + Mathf.Epsilon);
        return (this.Mesh.vertices[this.index(vector2.x, vector2.z)].y * (num - Vector3.Distance(vector2, vector)) + this.Mesh.vertices[this.index(vector3.x, vector3.z)].y * (num - Vector3.Distance(vector3, vector)) + this.Mesh.vertices[this.index(vector4.x, vector4.z)].y * (num - Vector3.Distance(vector4, vector)) + this.Mesh.vertices[this.index(vector5.x, vector5.z)].y * (num - Vector3.Distance(vector5, vector))) * base.transform.lossyScale.y / num2;
    }

    // Token: 0x060001DE RID: 478 RVA: 0x000095DC File Offset: 0x000077DC
    private Vector3[] GenerateVerts()
    {
        Vector3[] array = new Vector3[(this.Dimension + 1) * (this.Dimension + 1)];
        for (int i = 0; i <= this.Dimension; i++)
        {
            for (int j = 0; j <= this.Dimension; j++)
            {
                array[this.index(i, j)] = new Vector3((float)i, 0f, (float)j);
            }
        }
        return array;
    }

    // Token: 0x060001DF RID: 479 RVA: 0x00009640 File Offset: 0x00007840
    private int[] GenerateTries()
    {
        int[] array = new int[this.Mesh.vertices.Length * 6];
        for (int i = 0; i < this.Dimension; i++)
        {
            for (int j = 0; j < this.Dimension; j++)
            {
                array[this.index(i, j) * 6] = this.index(i, j);
                array[this.index(i, j) * 6 + 1] = this.index(i + 1, j + 1);
                array[this.index(i, j) * 6 + 2] = this.index(i + 1, j);
                array[this.index(i, j) * 6 + 3] = this.index(i, j);
                array[this.index(i, j) * 6 + 4] = this.index(i, j + 1);
                array[this.index(i, j) * 6 + 5] = this.index(i + 1, j + 1);
            }
        }
        return array;
    }

    // Token: 0x060001E0 RID: 480 RVA: 0x00009720 File Offset: 0x00007920
    private Vector2[] GenerateUVs()
    {
        Vector2[] array = new Vector2[this.Mesh.vertices.Length];
        for (int i = 0; i <= this.Dimension; i++)
        {
            for (int j = 0; j <= this.Dimension; j++)
            {
                Vector2 vector = new Vector2((float)i / this.UVScale % 2f, (float)j / this.UVScale % 2f);
                array[this.index(i, j)] = new Vector2((vector.x <= 1f) ? vector.x : (2f - vector.x), (vector.y <= 1f) ? vector.y : (2f - vector.y));
            }
        }
        return array;
    }

    // Token: 0x060001E1 RID: 481 RVA: 0x000097E6 File Offset: 0x000079E6
    private int index(int x, int z)
    {
        return x * (this.Dimension + 1) + z;
    }

    // Token: 0x060001E2 RID: 482 RVA: 0x000097F4 File Offset: 0x000079F4
    private int index(float x, float z)
    {
        return this.index((int)x, (int)z);
    }

    // Token: 0x060001E3 RID: 483 RVA: 0x00009800 File Offset: 0x00007A00
    private void Update()
    {
        Vector3[] vertices = this.Mesh.vertices;
        for (int i = 0; i <= this.Dimension; i++)
        {
            for (int j = 0; j <= this.Dimension; j++)
            {
                float num = 0f;
                for (int k = 0; k < this.Octaves.Length; k++)
                {
                    if (this.Octaves[k].alternate)
                    {
                        float num2 = Mathf.PerlinNoise((float)i * this.Octaves[k].scale.x / (float)this.Dimension, (float)j * this.Octaves[k].scale.y / (float)this.Dimension) * 3.1415927f * 2f;
                        num += Mathf.Cos(num2 + this.Octaves[k].speed.magnitude * Time.time) * this.Octaves[k].height;
                    }
                    else
                    {
                        float num3 = Mathf.PerlinNoise(((float)i * this.Octaves[k].scale.x + Time.time * this.Octaves[k].speed.x) / (float)this.Dimension, ((float)j * this.Octaves[k].scale.y + Time.time * this.Octaves[k].speed.y) / (float)this.Dimension) - 0.5f;
                        num += num3 * this.Octaves[k].height;
                    }
                }
                vertices[this.index(i, j)] = new Vector3((float)i, num, (float)j);
            }
        }
        this.Mesh.vertices = vertices;
        this.Mesh.RecalculateNormals();
    }

    // Token: 0x04000261 RID: 609
    public int Dimension = 10;

    // Token: 0x04000262 RID: 610
    public float UVScale = 2f;

    // Token: 0x04000263 RID: 611
    public Waves.Octave[] Octaves;

    // Token: 0x04000264 RID: 612
    protected MeshFilter MeshFilter;

    // Token: 0x04000265 RID: 613
    protected Mesh Mesh;

    // Token: 0x020000E7 RID: 231
    [Serializable]
    public struct Octave
    {
        // Token: 0x04000569 RID: 1385
        public Vector2 speed;

        // Token: 0x0400056A RID: 1386
        public Vector2 scale;

        // Token: 0x0400056B RID: 1387
        public float height;

        // Token: 0x0400056C RID: 1388
        public bool alternate;
    }
}

using System;
using UnityEngine;

// Token: 0x02000069 RID: 105
public class WeaponController : MonoBehaviour
{
    // Token: 0x060001FE RID: 510 RVA: 0x0000A083 File Offset: 0x00008283
    private void Start()
    {
        this.weaponAnimator = base.GetComponent<Animator>();
    }

    // Token: 0x060001FF RID: 511 RVA: 0x0000A094 File Offset: 0x00008294
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            this.weaponAnimator.SetTrigger("isAttacking");
        }
        if (this.newSparks != null)
        {
            this.newSparks.transform.Translate(this.sparksDirection * (float)this.sparksSpeed * Time.deltaTime);
        }
    }

    // Token: 0x06000200 RID: 512 RVA: 0x0000A0F4 File Offset: 0x000082F4
    public void SpawnSparks()
    {
        this.newSparks = Object.Instantiate<GameObject>(this.sparksEffect, this.sparksPos.position, Quaternion.identity);
        this.sparksDirection = this.sparksPos.transform.forward;
        Object.Instantiate<GameObject>(this.sounds[Random.Range(0, this.sounds.Length)], this.sparksPos.position, Quaternion.identity);
    }

    // Token: 0x04000285 RID: 645
    public GameObject sparksEffect;

    // Token: 0x04000286 RID: 646
    public GameObject newSparks;

    // Token: 0x04000287 RID: 647
    public Transform sparksPos;

    // Token: 0x04000288 RID: 648
    public int sparksSpeed;

    // Token: 0x04000289 RID: 649
    public GameObject[] sounds;

    // Token: 0x0400028A RID: 650
    private Animator weaponAnimator;

    // Token: 0x0400028B RID: 651
    private Vector3 sparksDirection;
}

using System;
using UnityEngine;

// Token: 0x0200004B RID: 75
public class YT : MonoBehaviour
{
    // Token: 0x06000184 RID: 388 RVA: 0x00007FA8 File Offset: 0x000061A8
    public void OnButtonDown()
    {
        Application.OpenURL("https://www.youtube.com/channel/UCmjNGJn0WNPJS_G52UfkH-g");
    }

    // Token: 0x06000185 RID: 389 RVA: 0x00007FB4 File Offset: 0x000061B4
    public void OnSteamButtonDown()
    {
        Application.OpenURL("https://store.steampowered.com/app/3898150/Gvalt/");
    }
}
// Привет! Для информации по играм ищи в Интернете по запросу "макстер {название игры} игра"
