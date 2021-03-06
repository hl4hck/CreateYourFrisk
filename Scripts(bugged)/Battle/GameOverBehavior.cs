﻿using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// The fairly hacky and somewhat unmaintainable Game Over behaviour class. Written in a hurry as it probably wasn't going to get replaced anytime soon.
/// This script is attached to the Player object to make it persist on scene switch, and immediately switches to the Game Over scene upon attachment.
/// There, the GameOverInit behaviour takes care of calling StartDeath() on this behaviour.
/// </summary>
public class GameOverBehavior : MonoBehaviour {
    private GameObject brokenHeartPrefab;
    private GameObject heartShardPrefab;
    private GameObject Player;
    private Transform playerParent;
    public static GameObject battleCamera;
    public static GameObject battleContainer;
    public static GameObject gameOverContainer;
    public static GameObject gameOverContainerOw;
    private string[] heartShardAnim = new string[] { "UI/Battle/heartshard_0", "UI/Battle/heartshard_1", "UI/Battle/heartshard_2", "UI/Battle/heartshard_3" };
    private TextManager gameOverTxt;
    private TextManager reviveText;
    private Image gameOverImage;
    private Image reviveFade;
    private Image reviveFade2;
    private RectTransform[] heartShardInstances = new RectTransform[0];
    private Vector2[] heartShardRelocs;
    private LuaSpriteController[] heartShardCtrl;
    
    private AudioSource gameOverMusic;

    private float breakHeartAfter = 1.0f;
    private bool  breakHeartReviveAfter = false;
    private float explodeHeartAfter = 2.5f;
    private float gameOverAfter = 4.5f;
    private float fluffybunsAfter = 6.5f;
    private float internalTimer = 0.0f;
    private float internalTimerRevive = 0.0f;
    private float gameOverFadeTimer = 0.0f;
    private bool started = false;
    private bool done = false;
    private bool exiting = false;
    private bool once = false;

    private Vector2 heartPos;
    private Color heartColor;

    //private bool overworld = false;
    private string deathMusic;
    private string[] deathText;

    public int playerIndex = -1;
    public int luaEncounterIndex = -1;
    public float playerZ = -1;
    public bool autolinebreakstate = false;
    public bool revived = false;
    public bool hasRevived = false;
    public bool reviveTextSet = false;
    public AudioSource musicBefore = null;
    public AudioClip music = null;

    public void ResetGameOver() {
        if (!UnitaleUtil.IsOverworld) {
            UIController.instance.encounter.gameOverStance = false;
            LuaEnemyEncounter.script.SetVar("autolinebreak", MoonSharp.Interpreter.DynValue.NewBoolean(autolinebreakstate));
        }
        heartShardInstances = new RectTransform[0];
        breakHeartAfter = 1.0f;
        breakHeartReviveAfter = false;
        explodeHeartAfter = 2.5f;
        gameOverAfter = 4.5f;
        fluffybunsAfter = 6.5f;
        internalTimer = 0.0f;
        internalTimerRevive = 0.0f;
        gameOverFadeTimer = 0.0f;
        started = false;
        done = false;
        exiting = false;
        once = false;
        //overworld = false;
        playerIndex = -1;
        luaEncounterIndex = -1;
        playerZ = -1;
        autolinebreakstate = false;
        revived = false;
        reviveTextSet = false;
    }

    public void Revive() { revived = true; }

    public void StartDeath(string[] deathText = null, string deathMusic = null) {
        if (!UnitaleUtil.IsOverworld) {
            UIController.instance.encounter.EndWave(true);
            autolinebreakstate = LuaEnemyEncounter.script.GetVar("autolinebreak").Boolean;
            LuaEnemyEncounter.script.SetVar("autolinebreak", MoonSharp.Interpreter.DynValue.NewBoolean(true));
        } else
            autolinebreakstate = true;

        this.deathText = deathText;
        this.deathMusic = deathMusic;
        
        if (!UnitaleUtil.IsOverworld)  Player = GameObject.Find("player");
        else                             Player = GameObject.Find("Player");

        playerZ = 130;
        playerParent = Player.transform.parent;
        playerIndex = Player.transform.GetSiblingIndex();
        Player.transform.SetParent(null);

        if (UnitaleUtil.IsOverworld) {
            Player.transform.position = new Vector3(Player.transform.position.x - GameObject.Find("Main Camera OW").transform.position.x - 320,
                                                    Player.transform.position.y - GameObject.Find("Main Camera OW").transform.position.y - 240, Player.transform.position.z);
            GameObject.Destroy(GameObject.Find("Main Camera OW"));
            Player.GetComponent<SpriteRenderer>().enabled = true; // stop showing the player
        } else {
            UIController.instance.encounter.gameOverStance = true;
            Player.GetComponent<PlayerController>().invulTimer = 0;
            Player.GetComponent<Image>().enabled = true; // abort the blink animation if it was playing
            battleCamera = GameObject.Find("Main Camera");
            battleCamera.SetActive(false);
        }

        /*battleContainer = new GameObject("BattleContainer");
        foreach (Transform go in UnitaleUtil.GetFirstChildren(null, false))
            if (go.name != battleContainer.name &&!go.GetComponent<LuaEnemyEncounter>() && go.name != Player.name &&!go.name.Contains("AudioChannel"))
                go.SetParent(battleContainer.transform);
        battleContainer.SetActive(false);*/

        if (UnitaleUtil.IsOverworld)
            gameOverContainerOw.SetActive(true);
        else
            gameOverContainer.SetActive(true);

        Camera.main.GetComponent<AudioSource>().clip = AudioClipRegistry.GetMusic("mus_gameover");
        GameObject.Find("GameOver").GetComponent<Image>().sprite = SpriteRegistry.Get("UI/spr_gameoverbg_0");

        if (UnitaleUtil.IsOverworld) {
            heartColor = GameObject.Find("utHeart").GetComponent<Image>().color;
            heartColor.a = 1;
        } else {
            heartColor = gameObject.GetComponent<Image>().color;
            gameObject.transform.SetParent(GameObject.Find("Canvas GameOver").transform);
        }

        //if (overworld)
        //    gameObject.transform.SetParent(GameObject.Find("Canvas OW").transform);
        //else
        gameObject.transform.SetParent(GameObject.Find("Canvas GameOver").transform);
        PlayerCharacter.instance.HP = PlayerCharacter.instance.MaxHP;
        brokenHeartPrefab = Resources.Load<GameObject>("Prefabs/heart_broken");
        heartShardPrefab = SpriteRegistry.GENERIC_SPRITE_PREFAB.gameObject;
        reviveText = GameObject.Find("ReviveText").GetComponent<TextManager>();
        reviveFade = GameObject.Find("ReviveFade").GetComponent<Image>();
        reviveFade.transform.SetAsLastSibling();
        gameOverTxt = GameObject.Find("TextParent").GetComponent<TextManager>();
        gameOverImage = GameObject.Find("GameOver").GetComponent<Image>();
        heartPos = gameObject.GetComponent<RectTransform>().position;
        gameOverMusic = Camera.main.GetComponent<AudioSource>();
        started = true;
    }

    void Awake() {
        //GameObject.Destroy(GameObject.Find("Canvas OW"));
        //GameObject.Destroy(GameObject.Find("Player"));
        //SceneManager.LoadScene("GameOver");
        //if (GameObject.Find("Canvas OW") != null)
        //    overworld = true;
        //if (overworld)
        //    GameObject.Destroy(GameObject.Find("Main Camera OW"));
    }

	// Update is called once per frame
	void Update () {
        if (hasRevived && reviveFade2) {
            if (reviveFade2.transform.localPosition != new Vector3(0, 0, 0))
                reviveFade2.transform.localPosition = new Vector3(0, 0, 0);
            if (reviveFade2.color.a > 0.0f)  reviveFade2.color = new Color(1, 1, 1, reviveFade2.color.a - Time.deltaTime / 2);
            else                             GameObject.Destroy(reviveFade2.gameObject);
        }
        if (!started)
            return;
        if (!revived) {
            if (!once && UnitaleUtil.IsOverworld) {
                once = true;
                GameObject.Find("utHeart").transform.SetParent(GameObject.Find("Canvas GameOver").transform);
                GameObject.Find("utHeart").transform.position = heartPos;
                GameObject.Find("utHeart").GetComponent<Image>().color = heartColor;
                GameObject.Destroy(GameObject.Find("Canvas OW"));
            } else if (!once) {
                once = true;
                gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(16, 16);
                gameObject.GetComponent<Image>().enabled = true; // abort the blink animation if it was playing
            }

            if (internalTimer > breakHeartAfter) {
                AudioSource.PlayClipAtPoint(AudioClipRegistry.GetSound("heartbeatbreaker"), Camera.main.transform.position, 0.75f);
                brokenHeartPrefab = Instantiate(brokenHeartPrefab);
                if (UnitaleUtil.IsOverworld)
                    brokenHeartPrefab.transform.SetParent(GameObject.Find("Canvas GameOver").transform);
                else
                    brokenHeartPrefab.transform.SetParent(gameObject.transform);
                brokenHeartPrefab.GetComponent<RectTransform>().position = heartPos;
                brokenHeartPrefab.GetComponent<Image>().color = heartColor;
                brokenHeartPrefab.GetComponent<Image>().enabled = true;
                if (UnitaleUtil.IsOverworld)
                    GameObject.Find("utHeart").GetComponent<Image>().enabled = false;
                else {
                    Color color = gameObject.GetComponent<Image>().color;
                    gameObject.GetComponent<Image>().color = new Color(color.r, color.g, color.b, 0);
                    if (LuaEnemyEncounter.script.GetVar("revive").Boolean)
                        Revive();
                }
                breakHeartAfter = 999.0f;
            }

            if (internalTimer > explodeHeartAfter) {
                AudioSource.PlayClipAtPoint(AudioClipRegistry.GetSound("heartsplosion"), Camera.main.transform.position, 0.75f);
                brokenHeartPrefab.GetComponent<Image>().enabled = false;
                heartShardInstances = new RectTransform[6];
                heartShardRelocs = new Vector2[6];
                heartShardCtrl = new LuaSpriteController[6];
                for (int i = 0; i < heartShardInstances.Length; i++) {
                    heartShardInstances[i] = Instantiate(heartShardPrefab).GetComponent<RectTransform>();
                    heartShardCtrl[i] = new LuaSpriteController(heartShardInstances[i].GetComponent<Image>());
                    if (UnitaleUtil.IsOverworld)
                        heartShardInstances[i].transform.SetParent(GameObject.Find("Canvas GameOver").transform);
                    else
                        heartShardInstances[i].transform.SetParent(this.gameObject.transform);
                    heartShardInstances[i].GetComponent<RectTransform>().position = heartPos;
                    heartShardInstances[i].GetComponent<Image>().color = heartColor;
                    heartShardRelocs[i] = UnityEngine.Random.insideUnitCircle * 100.0f;
                    heartShardCtrl[i].Set(heartShardAnim[0]);
                    heartShardCtrl[i].SetAnimation(heartShardAnim, 1 / 5f);
                }
                explodeHeartAfter = 999.0f;
            }

            if (internalTimer > gameOverAfter) {
                AudioClip originMusic = gameOverMusic.clip;
                if (deathMusic != null) {
                    gameOverMusic.clip = AudioClipRegistry.GetMusic(deathMusic);
                    if (gameOverMusic.clip == null) {
                        UnitaleUtil.WriteInLogAndDebugger("[WARN]The specified death music doesn't exist. (" + deathMusic + ")");

                        gameOverMusic.clip = originMusic;
                    }
                }
                gameOverMusic.Play();
                gameOverAfter = 999.0f;
            }

            if (internalTimer > fluffybunsAfter) {
                gameOverTxt.SetHorizontalSpacing(7);
                if (deathText != null) {
                    List<TextMessage> text = new List<TextMessage>();
                    foreach (string str in deathText)
                        text.Add(new TextMessage(str, false, false));
                    TextMessage[] text2 = new TextMessage[text.Count + 1];
                    for (int i = 0; i < text.Count; i++)
                        text2[i] = text[i];
                    text2[text.Count] = new TextMessage("", false, false);
                    if (Random.Range(0, 400) == 44)
                        gameOverTxt.SetTextQueue(new TextMessage[]{
                            new TextMessage("[color:ffffff][voice:v_fluffybuns][waitall:2]4", false, false),
                            new TextMessage("[color:ffffff][voice:v_fluffybuns][waitall:2]" + PlayerCharacter.instance.Name + "!\n[w:15]Stay determined...", false, false),
                            new TextMessage("", false, false) });
                    else
                        gameOverTxt.SetTextQueue(text2);
                } else {
                    //This "4" made us laugh so hard that I kept it :P
                    int fourChance = Random.Range(0, 80);

                    string[] possibleDeathTexts = new string[] { "You cannot give up\njust yet...", "It cannot end\nnow...", "Our fate rests upon\nyou...",
                                                                 "Don't lose hope...", "You're going to\nbe alright!"};
                    if (fourChance == 44)
                        possibleDeathTexts[4] = "4";

                    gameOverTxt.SetTextQueue(new TextMessage[]{
                        new TextMessage("[color:ffffff][voice:v_fluffybuns][waitall:2]" + possibleDeathTexts[Math.RandomRange(0, possibleDeathTexts.Length)], false, false),
                        new TextMessage("[color:ffffff][voice:v_fluffybuns][waitall:2]" + PlayerCharacter.instance.Name + "!\n[w:15]Stay determined...", false, false),
                        new TextMessage("", false, false) });                        }

                fluffybunsAfter = 999.0f;
            }

            if (!done) {
                gameOverImage.color = new Color(1, 1, 1, gameOverFadeTimer);
                if (gameOverAfter >= 999.0f && gameOverFadeTimer < 1.0f) {
                    gameOverFadeTimer += Time.deltaTime / 2;
                    if (gameOverFadeTimer >= 1.0f) {
                        gameOverFadeTimer = 1.0f;
                        done = true;
                    }
                }
                internalTimer += Time.deltaTime; // this is actually dangerous because done can be true before everything's done if timers are modified
            } else if (!exiting &&!gameOverTxt.AllLinesComplete())
                // Note: [noskip] only affects the UI controller's ability to skip, so we have to redo that here.
                if (InputUtil.Pressed(GlobalControls.input.Confirm) && gameOverTxt.LineComplete())
                    gameOverTxt.NextLineText();
        } else {
            /*if (internalTimer <= breakHeartAfter) {

            } else {*/
            if (reviveTextSet &&!reviveText.AllLinesComplete()) {
                // Note: [noskip] only affects the UI controller's ability to skip, so we have to redo that here.
                if (InputUtil.Pressed(GlobalControls.input.Confirm) && reviveText.LineComplete())
                    reviveText.NextLineText();
            } else if (reviveTextSet &&!exiting) {
                exiting = true;
            } else if (internalTimerRevive >= 5.0f &&!reviveTextSet && breakHeartReviveAfter) {
                if (deathText != null) {
                    reviveText.SetHorizontalSpacing(7);
                    List<TextMessage> text = new List<TextMessage>();
                    foreach (string str in deathText)
                        text.Add(new TextMessage(str, false, false));
                    TextMessage[] text2 = new TextMessage[text.Count + 1];
                    for (int i = 0; i < text.Count; i++)
                        text2[i] = text[i];
                    text2[text.Count] = new TextMessage("", false, false);
                    reviveText.SetTextQueue(text2);
                }
                reviveTextSet = true;
            } else if (internalTimerRevive > 2.5f && internalTimerRevive < 4.0f) {
                brokenHeartPrefab.transform.localPosition = new Vector2(UnityEngine.Random.Range(-3, 2), UnityEngine.Random.Range(-3, 2));
            } else if (!breakHeartReviveAfter && internalTimerRevive > 2.5f) {
                breakHeartReviveAfter = true;
                AudioSource.PlayClipAtPoint(AudioClipRegistry.GetSound("heartbeatbreaker"), Camera.main.transform.position, 0.75f);
                if (UnitaleUtil.IsOverworld)
                    GameObject.Find("utHeart").GetComponent<Image>().enabled = true;
                else {
                    Color color = gameObject.GetComponent<Image>().color;
                    gameObject.GetComponent<Image>().color = new Color(color.r, color.g, color.b, 1);
                }
                GameObject.Destroy(brokenHeartPrefab);
            }
            //}

            if (internalTimer > explodeHeartAfter) { }

            if (internalTimer > gameOverAfter) { }

            if (internalTimer > fluffybunsAfter) { }

            if (!done) { } 
            else if (!exiting &&!reviveText.AllLinesComplete()) { }

            if (!reviveTextSet) internalTimerRevive += Time.deltaTime;

            if (exiting && reviveFade.color.a < 1.0f && reviveFade.isActiveAndEnabled)
                reviveFade.color = new Color(1, 1, 1, reviveFade.color.a + Time.deltaTime / 2);
            else if (exiting) {
                // repurposing the timer as a reset delay
                gameOverFadeTimer -= Time.deltaTime;
                if (gameOverMusic.volume - Time.deltaTime > 0.0f) gameOverMusic.volume -= Time.deltaTime;
                else gameOverMusic.volume = 0.0f;
                if (gameOverFadeTimer < -1.0f) {
                    reviveFade2 = GameObject.Instantiate(reviveFade.gameObject).GetComponent<Image>();
                    Player.transform.SetParent(playerParent);
                    Player.transform.SetSiblingIndex(playerIndex);
                    Player.transform.position = new Vector3(Player.transform.position.x, Player.transform.position.y, playerZ);
                    reviveFade2.transform.SetParent(playerParent);
                    reviveFade2.transform.SetAsLastSibling();
                    reviveFade2.transform.localPosition = new Vector3(0, 0, 0);
                    reviveFade.color = new Color(1, 1, 1, 0);
                    ResetGameOver();
                    gameOverContainer.SetActive(false);
                    if (!UnitaleUtil.IsOverworld)
                        battleCamera.SetActive(true);
                    else {
                        GameObject cam = GameObject.Instantiate<GameObject>(Resources.Load<GameObject>("Prefabs/Main Camera OW"));
                        cam.name = "Main Camera OW";
                    }
                    if (musicBefore != null) {
                        musicBefore.clip = music;
                        musicBefore.Play();
                    }
                    hasRevived = true;
                }
            }
        }

        for (int i = 0; i < heartShardInstances.Length; i++) {
            heartShardInstances[i].position += (Vector3)heartShardRelocs[i]*Time.deltaTime;
            heartShardRelocs[i].y -= 100f * Time.deltaTime;
        }

        if (gameOverTxt.textQueue != null)
            if (!exiting && gameOverTxt.AllLinesComplete() && gameOverTxt.LineCount() != 0) {
                exiting = true;
                gameOverFadeTimer = 1.0f;
            } else if (exiting && gameOverFadeTimer > 0.0f) {
                gameOverImage.color = new Color(1, 1, 1, gameOverFadeTimer);
                if (gameOverFadeTimer > 0.0f)  {
                    gameOverFadeTimer -= Time.deltaTime / 2;
                    if (gameOverFadeTimer <= 0.0f)
                        gameOverFadeTimer = 0.0f;
                }
            }
            else if (exiting) {
                // repurposing the timer as a reset delay
                gameOverFadeTimer -= Time.deltaTime;
                if (gameOverMusic.volume - Time.deltaTime > 0.0f)
                    gameOverMusic.volume -= Time.deltaTime;
                else
                    gameOverMusic.volume = 0.0f;
                if (gameOverFadeTimer < -1.0f) {
                    //StaticInits.Reset();
                    Destroy(gameObject);
                    if (!GlobalControls.modDev)
                        SaveLoad.Load(false);
                    if (!UnitaleUtil.IsOverworld)
                        UIController.EndBattle(false);
                    else //{
                        GameObject.Destroy(gameOverContainer);
                    if (!GlobalControls.modDev)
                        SceneManager.LoadScene("TransitionOverworld");
                    else
                        SceneManager.LoadScene("ModSelect");
                    //}
                }
            }
	}
}
