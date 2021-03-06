﻿using System;
using System.Linq;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// TODO less code duplicate-y way of pulling commands out of the text.
public class TextManager : MonoBehaviour {
    internal Image[] letterReferences;
    internal Vector2[] letterPositions;

    protected UnderFont default_charset = null;
    protected AudioClip default_voice = null;
    public AudioSource letterSound = null;
    protected TextEffect textEffect;
    public List<Letter> letters = new List<Letter>();
    private string letterEffect = "none";
    private string[] commandList = new string[] { "color", "charspacing", "linespacing", "starcolor", "instant", "font", "effect", "noskip", "w", "waitall", "novoice",
                                                  "next", "finished", "nextthisnow", "noskipatall", "waitfor", "speed", "letters", "voice", "func", "mugshot", "name",
                                                  "music", "sound", "health", "lettereffect"};
    private float letterIntensity = 0.0f;
    public int currentLine = 0;
    private int currentCharacter = 0;
    public int currentReferenceCharacter = 0;
    private bool displayImmediate = false;
    private bool currentSkippable = true;
    public bool nextMonsterDialogueOnce = false, nmd2 = false, wasStated = false;
    private RectTransform self;
    public Vector2 offset;
    private bool offsetSet = false;
    private float currentX;
    //private float _currentY;
    private float currentY; /* {
        get { return _currentY; }
        set {
            if (GetType() == typeof(LuaTextManager))
                print("Change currentY value: " + _currentY + " => " + value);
            _currentY = value;
        }
    }*/
    private bool paused = false;
    private bool muted = false;
    private bool autoSkipThis = false;
    private bool autoSkipAll = false;
    private bool autoSkip = false;
    private bool instantCommand = false;
    private bool skipFromPlayer = false;
    private bool firstChar = false;
    internal float hSpacing = 3;
    internal float vSpacing = 0;
    private LuaSpriteController mugshot;
    private string[] mugshotList = null;
    private string finalMugshot;
    private float mugshotTimer;
    private int letterSpeed = 1;
    private int letterOnceValue = 0;
    private KeyCode waitingChar = KeyCode.None;

    private Color currentColor = Color.white;
    //private Color defaultColor = Color.white;

    private float letterTimer = 0.0f;
    private float timePerLetter;
    private float singleFrameTiming = 1.0f / 30;

    private ScriptWrapper caller;

    public UnderFont Charset { get; private set; }
    public TextMessage[] textQueue = null;
    //public string[] mugshotsPath;
    //public bool overworld;
    public bool blockSkip = false;
    public bool hidden = false;
    public bool skipNowIfBlocked = false;
    internal bool noSkip1stFrame = true;

    public void SetCaller(ScriptWrapper s) { caller = s; }

    public void SetFont(UnderFont font, bool firstTime = false) {
        Charset = font;
        if (default_charset == null)
            default_charset = font;
        if (firstTime) {
            if (letterSound == null)          letterSound.clip = font.Sound;
            if (currentColor == Color.white)  currentColor = font.DefaultColor;
            if (hSpacing == 3)                hSpacing = font.CharSpacing;
        } else {
            letterSound.clip = font.Sound;
            currentColor = font.DefaultColor;
            hSpacing = font.CharSpacing;
        }
    }

    public void SetHorizontalSpacing(float spacing = 3) { hSpacing = spacing; }

    public void SetVerticalSpacing(float spacing = 0) { this.vSpacing = spacing; }

    public void ResetFont() {
        if (Charset == null || default_charset == null)
            if (GetType() == typeof(LuaTextManager))
                ((LuaTextManager)this).SetFont(SpriteFontRegistry.UI_DEFAULT_NAME);
            else
                SetFont(SpriteFontRegistry.Get(SpriteFontRegistry.UI_DEFAULT_NAME), true);
        Charset = default_charset;
        if (default_voice != null)
            letterSound.clip = default_voice;
        else
            letterSound.clip = default_charset.Sound;
    }

    protected virtual void Awake() {
        self = gameObject.GetComponent<RectTransform>();
        letterSound = gameObject.AddComponent<AudioSource>();
        letterSound.playOnAwake = false;
        // SetFont(SpriteFontRegistry.F_UI_DIALOGFONT);
        timePerLetter = singleFrameTiming;

        if (UnitaleUtil.IsOverworld && GameObject.Find("textframe_border_outer"))
            mugshot = new LuaSpriteController(GameObject.Find("Mugshot").GetComponent<Image>());
    }

    private void Start() {
        // ResetFont();
        // SetText("the quick brown fox jumps over\rthe lazy dog.\nTHE QUICK BROWN FOX JUMPS OVER\rTHE LAZY DOG.\nJerry.", true, true);
        // SetText(new TextMessage("Here comes Napstablook.", true, false));
        // SetText(new TextMessage(new string[] { "Check", "Compliment", "Ignore", "Steal", "trow temy", "Jerry" }, false));
    }

    public void SetPause(bool pause) { this.paused = pause; }

    public bool IsPaused() { return this.paused; }

    public bool IsFinished() {
        if (letterReferences == null)
            return false;
        return currentCharacter >= letterReferences.Length;
    }

    public void SetMute(bool muted) { this.muted = muted; }

    public void SetText(TextMessage text) { SetTextQueue(new TextMessage[] { text }); }

    public void SetTextQueue(TextMessage[] textQueue) {
        ResetFont();
        this.textQueue = textQueue;
        currentLine = 0;
        ShowLine(0);
    }

    public void SetTextQueueAfterValue(int BeginText) {
        ResetFont();
        currentLine = BeginText;
        ShowLine(BeginText);
    }

    public void ResetCurrentCharacter() {
        currentCharacter = 0;
        currentReferenceCharacter = 0;
    }

    public void AddToTextQueue(TextMessage text) { AddToTextQueue(new TextMessage[] { text }); }

    public void AddToTextQueue(TextMessage[] textQueueToAdd) {
        if (AllLinesComplete())
            SetTextQueue(textQueueToAdd);
        else {
            int length = textQueue.Length + textQueueToAdd.Length;
            TextMessage[] newTextQueue = new TextMessage[length];
            textQueue.CopyTo(newTextQueue, 0);
            textQueueToAdd.CopyTo(newTextQueue, textQueue.Length);
            textQueue = newTextQueue;
        }
    }

    public bool CanSkip() { return currentSkippable; }

    public bool CanAutoSkip() { return autoSkip; }
    public bool CanAutoSkipThis() { return autoSkipThis; }
    public bool CanAutoSkipAll() { return autoSkipAll; }

    public int LineCount() {
        if (textQueue == null)
            return 0;
        return textQueue.Length;
    }

    public void SetOffset(float xOff, float yOff) {
        offset = new Vector2(xOff, yOff);
        offsetSet = true;
    }

    public bool LineComplete() {
        if (letterReferences == null)
            return false;
        return (displayImmediate || currentCharacter == letterReferences.Length);
    }

    public bool AllLinesComplete() {
        if (textQueue == null)  return true;
        else                    return currentLine == textQueue.Length - 1 && LineComplete();
    }

    private void SetMugshot(DynValue text) {
        List<string> mugshots = new List<string>();
        float time = -1;
        finalMugshot = null;
        if (text != null) {
            if (text.String != "null")
                if (text.Type == DataType.Table) {
                    int count = 0;
                    foreach (DynValue dv in text.Table.Values) {
                        count++;
                        if (dv.Type == DataType.Number && count >= text.Table.Length - 1 && time == -1)
                            time = (float)dv.Number;
                        else if (time != -1)
                            finalMugshot = "mugshots/" + dv.String;
                        else
                            mugshots.Add("mugshots/" + dv.String);
                    }
                } else
                    mugshots.Add("mugshots/" + text.String);
            else
                mugshots.Add("mugshots/");
        } else
            mugshots.Add("mugshots/");
        if (mugshot != null)
            if ((mugshots.Count > 1 || (mugshots[0] != "mugshots/" && mugshots[0] != "mugshots/null")) && text != null) {
                if (mugshots.Count > 1) {
                    mugshot.SetAnimation((string[])UnitaleUtil.ListToArray(mugshots), time);
                    if (finalMugshot == null)
                        finalMugshot = mugshots[mugshots.Count - 1];
                } else
                    mugshot.Set(mugshots[0]);
                mugshotTimer = time;
                mugshotList = (string[])UnitaleUtil.ListToArray(mugshots);
                mugshot.color = new float[] { 1, 1, 1, 1 };
                self.localPosition = new Vector3(-150, self.localPosition.y, self.localPosition.z);
            } else {
                mugshot.Set("empty");
                mugshotList = null;
                mugshot.color = new float[] { 1, 1, 1, 0 };
                if (gameObject.name == "TextManager OW")
                    self.localPosition = new Vector3(-267, self.localPosition.y, self.localPosition.z);
            }
    }

    protected void ShowLine(int line) {
        /*if (overworld) {
            if (mugshotsPath != null)
                if (mugshotsPath[line] != null || mugshotsPath[line] != "")
                    mugshot.sprite = SpriteRegistry.GetMugshot(mugshotsPath[line]);
                else
                    mugshot.sprite = null;

                if (mugshot.sprite == null) {
                    mugshot.color = new Color(mugshot.color.r, mugshot.color.g, mugshot.color.b, 0);
                    self.localPosition = new Vector3(-267, self.localPosition.y, self.localPosition.z);
                } else {
                    mugshot.color = new Color(mugshot.color.r, mugshot.color.g, mugshot.color.b, 1);
                    self.localPosition = new Vector3(-150, self.localPosition.y, self.localPosition.z);
                }
        }*/
        if (textQueue != null)
            if (line < textQueue.Length)
                if (textQueue[line] != null) {
                    //print("Mugshot just before condition: " + textQueue[line].Mugshot.ToString());
                    SetMugshot(textQueue[line].Mugshot);
                    if (UnitaleUtil.IsOverworld&& GameObject.Find("textframe_border_outer")) {
                        if (textQueue[line].ActualText) {
                            if (GameObject.Find("textframe_border_outer").GetComponent<Image>().color.a == 0)
                                SetTextFrameAlpha(1);
                            blockSkip = false;
                        } else {
                            if ((GameObject.Find("textframe_border_outer").GetComponent<Image>().color.a == 1))
                                SetTextFrameAlpha(0);
                            blockSkip = true;
                        }
                    }

                    if (!offsetSet)
                        SetOffset(0, 0);
                    currentColor = Charset.DefaultColor;
                    currentSkippable = true;
                    autoSkipThis = false;
                    autoSkip = false;
                    autoSkipAll = false;
                    instantCommand = false;
                    skipFromPlayer = false;
                    firstChar = false;
                    letterSound.clip = Charset.Sound;
                    timePerLetter = singleFrameTiming;
                    letterTimer = 0.0f;
                    DestroyText();
                    currentLine = line;
                    currentX = self.position.x + offset.x;
                    currentY = self.position.y + offset.y - Charset.LineSpacing;
                    /*if (GetType() == typeof(LuaTextManager))
                        print("currentY from ShowLine (" + textQueue[currentLine].Text + ") = " + self.position.y + " + " + offset.y + " - " + Charset.LineSpacing + " = " + currentY);*/
                    currentCharacter = 0;
                    currentReferenceCharacter = 0;
                    letterEffect = "none";
                    textEffect = null;
                    letterIntensity = 0;
                    letterSpeed = 1;
                    displayImmediate = textQueue[line].ShowImmediate;
                    SpawnText();
                    //if (!overworld)
                    //    UIController.instance.encounter.CallOnSelfOrChildren("AfterText");
                    if (UnitaleUtil.IsOverworld&& GameObject.Find("textframe_border_outer")) {
                        if (textQueue[line].ActualText) {
                            if (GameObject.Find("textframe_border_outer").GetComponent<Image>().color.a == 0)
                                SetTextFrameAlpha(1);
                        } else {
                            if ((GameObject.Find("textframe_border_outer").GetComponent<Image>().color.a == 1))
                                SetTextFrameAlpha(0);
                            DestroyText();
                        }
                        int lines = textQueue[line].Text.Split('\n').Length;
                        if (lines >= 4) lines = 4;
                        else            lines = 3;
                        Vector3 pos = GameObject.Find("TextManager OW").GetComponent<RectTransform>().localPosition;
                        GameObject.Find("TextManager OW").GetComponent<RectTransform>().localPosition = new Vector3(pos.x, 22 + ((lines - 1) * Charset.LineSpacing / 2), pos.z);
                    }
                }
    }

    public void SetTextFrameAlpha(float a) {
        Image[] imagesChild = null;
        Image[] images = null;

        if (UnitaleUtil.IsOverworld) {
            imagesChild = GameObject.Find("textframe_border_outer").GetComponentsInChildren<Image>();
            images = new Image[imagesChild.Length + 1];
            images[0] = GameObject.Find("textframe_border_outer").GetComponent<Image>();
        } else {
            imagesChild = GameObject.Find("arena_border_outer").GetComponentsInChildren<Image>();
            images = new Image[imagesChild.Length + 1];
            images[0] = GameObject.Find("arena_border_outer").GetComponent<Image>();
        }

        imagesChild.CopyTo(images, 1);

        foreach (Image img in images)
            img.color = new Color(img.color.r, img.color.g, img.color.b, a);
    }

    public bool HasNext() { return currentLine + 1 < LineCount(); }

    public void NextLineText() { ShowLine(++currentLine); }

    public void SkipText() {
        if (!noSkip1stFrame) {
            while (currentCharacter < letterReferences.Length) {
                if (letterReferences[currentCharacter] != null && Charset.Letters.ContainsKey(textQueue[currentLine].Text[currentCharacter])) {
                    letterReferences[currentCharacter].enabled = true;
                    currentReferenceCharacter++;
                }
            
                currentCharacter++;
            }
        }
    }

    public void DoSkipFromPlayer() {
        skipFromPlayer = true;
        if (LuaEnemyEncounter.script.GetVar("playerskipdocommand").Boolean)
            instantCommand = true;
        else
            SkipText();
    }

    public void SkipLine() {
        if (!noSkip1stFrame)
            while (currentCharacter < letterReferences.Length) {
                if (letterReferences[currentCharacter] != null && Charset.Letters.ContainsKey(textQueue[currentLine].Text[currentCharacter])) {
                    letterReferences[currentCharacter].enabled = true;
                    currentReferenceCharacter++;
                    if (textQueue[currentLine].Text[currentCharacter] == '\t') {
                        float indice = currentX / 320f;
                        if (currentX - (indice * 320f) < 36)
                            indice--;
                        currentX = indice * 320 + 356;
                    } else if (textQueue[currentLine].Text[currentCharacter] == '\n') {
                        currentX = self.position.x + offset.x;
                        /*if (GetType() == typeof(LuaTextManager))
                            print("currentY from \\n (" + textQueue[currentLine].Text + ") = " + currentY + " - " + vSpacing + " - " + Charset.LineSpacing + " = " + (currentY - vSpacing - Charset.LineSpacing));*/
                        currentY = currentY - vSpacing - Charset.LineSpacing;
                        currentCharacter++;
                        return;
                    }
                }
                currentCharacter++;
            }
    }

    public int CharacterCount(string str) {
        int count = 0;
        int bracketCount = 0;
        int currentChar = -1;
        string commandTest = "";
        bool resetted = false;
        do {
            for (int i = 0; i < str.Length; i++) {
                if (resetted) {
                    i = currentChar + 1;
                    count ++;
                    resetted = false;
                    bracketCount --;
                }
                if (str[i] == '[' && currentChar != i) {
                    if (bracketCount == 0)
                        currentChar = i;
                    bracketCount++;
                } else if (str[i] == '[' && bracketCount == 0 && currentChar == i)
                    currentChar = -1;
                if (bracketCount == 0)
                    count++;
                else if (bracketCount == 1 && (str[i] != '[' && str[i] != ']'))
                    commandTest += str[i];
                if (str[i] == ']') {
                    bracketCount = bracketCount == 0 ? 0 : bracketCount - 1;
                    if (bracketCount == 0 && currentChar != -1)
                        if (!commandList.Contains(commandTest.Split(':')[0])) {
                            i = currentChar - 1;
                            commandTest = "";
                        }
                }
            }
            resetted = true;
        } while (bracketCount > 0);
        
        return count;
    }

    public void SetEffect(TextEffect effect) { textEffect = effect; }

    public void DestroyText() {
        foreach (Transform child in gameObject.transform)
            Destroy(child.gameObject);
    }

    private void SpawnTextSpaceTest(int i, string currentText, out string currentText2) {
        int finalIndex = i + 1, beginIndex = i;

        for (; beginIndex > 0; beginIndex--)
            if (currentText[beginIndex] == '\n' || currentText[beginIndex] == '\r')
                break;
        for (; finalIndex < currentText.Length - 1; finalIndex++)
            if (currentText[finalIndex] == ' ' || currentText[finalIndex] == '\n' || currentText[finalIndex] == '\r')
                break;

        if (currentText[beginIndex] == '\n' || currentText[beginIndex] == ' ' || currentText[beginIndex] == '\r' ) beginIndex++;
        if (currentText[finalIndex] == '\n' || currentText[finalIndex] == ' ' || currentText[finalIndex] == '\r')  finalIndex--;
        
        int limit = 0;
        if (SceneManager.GetActiveScene().name == "Intro")          limit = 400;
        else if (UnitaleUtil.IsOverworld&& mugshot != null) {
            if (mugshot.alpha != 0)                                 limit = 417;
            else                                                    limit = 534;
        } else if (GlobalControls.isInFight) {
            if (UIController.instance.inited) {
                if (UIController.instance.encounter.gameOverStance) limit = 320;
                else if (name == "DialogBubble(Clone)")             limit = (int)transform.parent.GetComponent<LuaEnemyController>().bubbleWideness;
                else if (GetType() == typeof(LuaTextManager))       limit = gameObject.GetComponent<LuaTextManager>().textWidth;
                else                                                limit = 534;
            } else                                                  limit = 534;
        } else                                                      limit = 534;
        if (UnitaleUtil.CalcTotalLength(this, beginIndex, finalIndex) > limit && limit > 0) {
            int realBeginIndex = beginIndex, realFinalIndex = finalIndex;
            beginIndex = finalIndex - 1;
            while (textQueue[currentLine].Text[beginIndex] != ' ' && textQueue[currentLine].Text[beginIndex] != '\n' && textQueue[currentLine].Text[beginIndex] != '\r' && beginIndex > 0)
                beginIndex--;
            if (textQueue[currentLine].Text[beginIndex] == ' ' || textQueue[currentLine].Text[beginIndex] == '\n' || textQueue[currentLine].Text[beginIndex] == '\r' || beginIndex < 0)
                beginIndex++;
            if (UnitaleUtil.CalcTotalLength(this, beginIndex, finalIndex) > limit) {
                finalIndex = beginIndex;
                int testFinal = finalIndex;
                beginIndex = realBeginIndex;
                string currentText3 = currentText;
                for (; finalIndex <= realFinalIndex && finalIndex < currentText3.Length; finalIndex++)
                    if (UnitaleUtil.CalcTotalLength(this, beginIndex, finalIndex) > limit) {
                        if (finalIndex == testFinal) {
                            currentX = self.position.x + offset.x;
                            /*if (GetType() == typeof(LuaTextManager))
                                print("currentY from \\n (" + textQueue[currentLine].Text + ") = " + currentY + " - " + vSpacing + " - " + Charset.LineSpacing + " = " + (currentY - vSpacing - Charset.LineSpacing));*/
                            currentY = currentY - vSpacing - Charset.LineSpacing;
                            if (!UnitaleUtil.IsOverworld) {
                                if (SceneManager.GetActiveScene().name == "Intro")
                                    currentText3 = currentText3.Substring(0, finalIndex - 1) + "\n" + currentText3.Substring(finalIndex, currentText.Length - finalIndex);
                                else if (name == "DialogBubble(Clone)" || UIController.instance.encounter.gameOverStance || GetType() == typeof(LuaTextManager))
                                    currentText3 = currentText3.Substring(0, finalIndex - 1) + "\n" + currentText3.Substring(finalIndex, currentText.Length - finalIndex);
                                else
                                    currentText3 = currentText3.Substring(0, finalIndex - 1) + "\n  " + currentText3.Substring(finalIndex, currentText.Length - finalIndex);
                            } else {
                                currentText3 = currentText3.Substring(0, finalIndex - 1) + "\n  " + currentText3.Substring(finalIndex, currentText.Length - finalIndex);
                                realFinalIndex += 2;
                            }
                        } else {
                            if (!UnitaleUtil.IsOverworld) {
                                if (SceneManager.GetActiveScene().name == "Intro") {
                                    currentText3 = currentText3.Substring(0, finalIndex - 1) + "\n" + currentText3.Substring(finalIndex - 1, currentText.Length - finalIndex + 1);
                                    realFinalIndex++;
                                } else if (name == "DialogBubble(Clone)" || UIController.instance.encounter.gameOverStance || GetType() == typeof(LuaTextManager)) {
                                    currentText3 = currentText3.Substring(0, finalIndex - 1) + "\n" + currentText3.Substring(finalIndex - 1, currentText.Length - finalIndex + 1);
                                    realFinalIndex++;
                                } else {
                                    currentText3 = currentText3.Substring(0, finalIndex - 1) + "\n  " + currentText3.Substring(finalIndex - 1, currentText.Length - finalIndex + 1);
                                    realFinalIndex += 3;
                                }
                            } else {
                                currentText3 = currentText3.Substring(0, finalIndex - 1) + "\n  " + currentText3.Substring(finalIndex - 1, currentText.Length - finalIndex + 1);
                                realFinalIndex += 3;
                            }
                        }
                        Array.Resize(ref letterReferences, currentText3.Length);
                        Array.Resize(ref letterPositions, currentText3.Length);
                        beginIndex = finalIndex;
                    }
                currentText2 = currentText3;
                return;
            }
            beginIndex--;
            if (!UnitaleUtil.IsOverworld) {
                if (SceneManager.GetActiveScene().name == "Intro")
                    currentText2 = currentText.Substring(0, beginIndex) + "\n" + currentText.Substring(beginIndex + 1, currentText.Length - beginIndex - 1);
                else if (name == "DialogBubble(Clone)" || UIController.instance.encounter.gameOverStance || GetType() == typeof(LuaTextManager))
                    currentText2 = currentText.Substring(0, beginIndex) + "\n" + currentText.Substring(beginIndex + 1, currentText.Length - beginIndex - 1);
                else {
                    currentText2 = currentText.Substring(0, beginIndex) + "\n  " + currentText.Substring(beginIndex + 1, currentText.Length - beginIndex - 1);
                    Array.Resize(ref letterReferences, currentText2.Length);
                    Array.Resize(ref letterPositions, currentText2.Length);
                }
            } else {
                currentText2 = currentText.Substring(0, beginIndex) + "\n  " + currentText.Substring(beginIndex + 1, currentText.Length - beginIndex - 1);
                Array.Resize(ref letterReferences, currentText2.Length);
                Array.Resize(ref letterPositions, currentText2.Length);
            }
            currentX = self.position.x + offset.x;
            /*if (GetType() == typeof(LuaTextManager))
                print("currentY from \\n (" + textQueue[currentLine].Text + ") = " + currentY + " - " + vSpacing + " - " + Charset.LineSpacing + " = " + (currentY - vSpacing - Charset.LineSpacing));*/
            currentY = currentY - vSpacing - Charset.LineSpacing;
        } else
            currentText2 = currentText;
    }

    private void SpawnText() {
        letters.Clear();
        noSkip1stFrame = true;
        string currentText = textQueue[currentLine].Text;
        letterReferences = new Image[currentText.Length];
        letterPositions = new Vector2[currentText.Length];
        if (currentText.Length > 1)
            if (currentText[1] != ' ')
                if (!GlobalControls.isInFight) {
                    string currentText2;
                    SpawnTextSpaceTest(0, currentText, out currentText2);
                    if (currentText != currentText2)
                        textQueue[currentLine].Text = currentText = currentText2;
                } else if (LuaEnemyEncounter.script.GetVar("autolinebreak").Boolean || GetType() == typeof(LuaTextManager)) {
                    string currentText2;
                    SpawnTextSpaceTest(0, currentText, out currentText2);
                    if (currentText != currentText2)
                        textQueue[currentLine].Text = currentText = currentText2;
                }
        for (int i = 0; i < currentText.Length; i++) {
            switch (currentText[i]) {
                case '[':
                    int currentChar = i;
                    string command = ParseCommandInline(currentText, ref i);
                    if (command != null) {
                        if (commandList.Contains(command.Split(':')[0])) {
                            PreCreateControlCommand(command);
                            continue;
                        } else
                            i = currentChar;
                    } else
                        i = currentChar;
                    break;
                case '\n':
                    currentX = self.position.x + offset.x;
                    /*if (GetType() == typeof(LuaTextManager))
                        print("currentY from \\n = (" + textQueue[currentLine].Text + ") " + currentY + " - " + vSpacing + " - " + Charset.LineSpacing + " = " + (currentY - vSpacing - Charset.LineSpacing));*/
                    currentY = currentY - vSpacing - Charset.LineSpacing;
                    break;
                case '\t':
                    currentX = 356 + Camera.main.transform.position.x - 320; // HACK: bad tab usage
                    break;
                case ' ':
                    if (i + 1 == currentText.Length)
                        break;
                    if (currentText[i + 1] == ' ' )
                        break;
                    if (!GlobalControls.isInFight) {
                        string currentText2;
                        SpawnTextSpaceTest(i, currentText, out currentText2);
                        if (currentText != currentText2)
                            textQueue[currentLine].Text = currentText = currentText2;
                    } else if (LuaEnemyEncounter.script.GetVar("autolinebreak").Boolean || GetType() == typeof(LuaTextManager)) {
                        string currentText2;
                        SpawnTextSpaceTest(i, currentText, out currentText2);
                        if (currentText != currentText2)
                            textQueue[currentLine].Text = currentText = currentText2;
                    }
                    break;
            }

            if (!Charset.Letters.ContainsKey(currentText[i]))
                continue;
            GameObject singleLtr = null;
            try {
                singleLtr = Instantiate(SpriteFontRegistry.LETTER_OBJECT);
            } catch { }
            singleLtr.SetActive(true);
            RectTransform ltrRect = singleLtr.GetComponent<RectTransform>();
            ltrRect.localScale = new Vector3(1.001f, 1.001f, ltrRect.localScale.z);

            Image ltrImg = singleLtr.GetComponent<Image>();
            ltrRect.SetParent(gameObject.transform);
            ltrImg.sprite = Charset.Letters[currentText[i]];

            letterReferences[i] = ltrImg;
            
            ltrRect.position = new Vector3(currentX + .1f, (currentY + Charset.Letters[currentText[i]].border.w - Charset.Letters[currentText[i]].border.y + 2) + .1f, 0);
            /*if (GetType() == typeof(LuaTextManager))
                print("currentY from SpawnText (" + textQueue[currentLine].Text + ") = " + currentY + " + " + Charset.Letters[currentText[i]].border.w + " - " + Charset.Letters[currentText[i]].border.y + " + 2 = " + (currentY + Charset.Letters[currentText[i]].border.w - Charset.Letters[currentText[i]].border.y + 2)); */

            letterPositions[i] = ltrRect.anchoredPosition;
            ltrImg.SetNativeSize();
            if (GetType() == typeof(LuaTextManager)) {
                Color c = ((LuaTextManager)this)._color;
                if (currentColor == Color.white) ltrImg.color = c;
                else                             ltrImg.color = currentColor;
            } else                               ltrImg.color = currentColor;
            ltrImg.GetComponent<Letter>().colorFromText = currentColor;
            ltrImg.enabled = displayImmediate;
            letters.Add(singleLtr.GetComponent<Letter>());

            currentX += ltrRect.rect.width + hSpacing; // TODO remove hardcoded letter offset
        }
        if (UnitaleUtil.IsOverworld&& SceneManager.GetActiveScene().name != "TitleScreen" && SceneManager.GetActiveScene().name != "EnterName" && !GlobalControls.isInShop)
            if (mugshot.alpha == 0)
                mugshot.color = new float[] { 1, 1, 1, 0 };
        Update();
    }

    private bool CheckCommand() {
        if (currentLine >= textQueue.Length)
            return false;
        if (currentCharacter < textQueue[currentLine].Text.Length)
            if (textQueue[currentLine].Text[currentCharacter] == '[') {
                int currentChar = currentCharacter;
                string command = ParseCommandInline(textQueue[currentLine].Text, ref currentCharacter);
                if (command != null) {
                    currentCharacter++; // we're not in a continuable loop so move to the character after the ] manually

                    //float lastLetterTimer = letterTimer; // kind of a dirty hack so we can at least release 0.2.0 sigh
                    //float lastTimePerLetter = timePerLetter; // i am so sorry
                    
                    wasStated = false;

                    DynValue commandDV = DynValue.NewString(command);
                    if (commandList.Contains(commandDV.String.Split(':')[0]))
                        InUpdateControlCommand(commandDV);
                    else {
                        currentCharacter = currentChar;
                        return false;
                    }
                    //if (lastLetterTimer != letterTimer || lastTimePerLetter != timePerLetter)
                    //if (currentCharacter >= textQueue[currentLine].Text.Length)
                    //    return true;

                    return true;
                }
                currentCharacter = currentChar;

            }
        return false;
    }

    protected virtual void Update() {
        if (!UnitaleUtil.IsOverworld && nextMonsterDialogueOnce) {
            bool test = true;
            foreach (TextManager mgr in UIController.instance.monDialogues) {
                if (!mgr.IsFinished())
                    test = false;
            }
            if (test) {
                if (!wasStated)
                    UIController.instance.DoNextMonsterDialogue(true);
                wasStated = false;
                nextMonsterDialogueOnce = false;
            }
        } else if (mugshot != null && mugshotList != null)
            if (UnitaleUtil.IsOverworld&& mugshot.alpha != 0 && mugshotList.Length > 1) {
                if (!mugshot.animcomplete && (letterTimer < 0 || LineComplete())) {
                    mugshot.StopAnimation();
                    mugshot.Set(finalMugshot);
                } else if (mugshot.animcomplete && !(letterTimer < 0 || LineComplete()))
                    mugshot.SetAnimation(mugshotList, mugshotTimer);
            }

        if (textQueue == null || textQueue.Length == 0)
            return;
        if (paused)
            return;
        /*if (currentLine >= lineCount() && overworld) {
            endTextEvent();
            return;
        }*/

        if (textEffect != null)
            textEffect.UpdateEffects();

        if (displayImmediate)
            return;

        if (currentCharacter >= letterReferences.Length)
            return;

        if (waitingChar != KeyCode.None) {
            if (Input.GetKeyDown(waitingChar))
                waitingChar = KeyCode.None;
            else
                return;
        }

        letterTimer += Time.deltaTime;
        if ((letterTimer > timePerLetter || firstChar) && !LineComplete()) {
            firstChar = false;
            letterTimer = 0.0f;
            bool soundPlayed = false;
            int lastLetter = -1;
            if (letterOnceValue != 0 && !instantCommand) {
                if (!HandleShowLetter(ref soundPlayed, ref lastLetter))
                    return;
                letterOnceValue--;
            } else
                for (int i = 0; (instantCommand || i < letterSpeed) && currentCharacter < letterReferences.Length; i++)
                    if (!HandleShowLetter(ref soundPlayed, ref lastLetter))
                        return;
        }
        noSkip1stFrame = false;
    }

    private bool HandleShowLetter(ref bool soundPlayed, ref int lastLetter) {
        if (lastLetter != currentCharacter) {
            float oldLetterTimer = letterTimer;
            lastLetter = currentCharacter;
            while (CheckCommand()) {
                if (displayImmediate || letterTimer != oldLetterTimer)
                    return false;
            }
            if (currentCharacter >= letterReferences.Length)
                return false;
        }
        if (letterReferences[currentCharacter] != null) {
            letterReferences[currentCharacter].enabled = true;
            switch (letterEffect.ToLower()) {
                case "twitch": letterReferences[currentCharacter].GetComponent<Letter>().effect = new TwitchEffectLetter(letterReferences[currentCharacter].GetComponent<Letter>(), letterIntensity);   break;
                case "rotate": letterReferences[currentCharacter].GetComponent<Letter>().effect = new RotatingEffectLetter(letterReferences[currentCharacter].GetComponent<Letter>(), letterIntensity); break;
                case "shake":  letterReferences[currentCharacter].GetComponent<Letter>().effect = new ShakeEffectLetter(letterReferences[currentCharacter].GetComponent<Letter>(), letterIntensity);    break;
                default:       letterReferences[currentCharacter].GetComponent<Letter>().effect = null;                                                                                                 break;
            }
            if (letterSound != null && !muted && !soundPlayed) {
                soundPlayed = true;
                if (letterSound.isPlaying) UnitaleUtil.PlaySound("BubbleSound", letterSound.clip.name);
                else                       letterSound.Play();
            }
        }
        currentReferenceCharacter++;
        currentCharacter++;
        return true;
    }

    private string OldParseCommandInline(string input, ref int currentChar) {
        currentChar++; // skip past found bracket
        CheckCharInBounds(currentChar, input.Length);
        string control = "";
        while (input[currentChar] != ']') {
            control += input[currentChar];
            currentChar++;
            CheckCharInBounds(currentChar, input.Length);
        }
        return control;
    }

    private string ParseCommandInline(string input, ref int currentChar) {
        currentChar++; // skip past found bracket
        if (CheckCharInBounds(currentChar, input.Length))
            return null;
        string control = ""; int count = 1;
        while (true) {
            if (input[currentChar] == '[')
                count++;
            else if (input[currentChar] == ']') {
                count--;
                if (count == 0)
                    break;
            }
            control += input[currentChar];
            currentChar++;
            if (CheckCharInBounds(currentChar, input.Length))
                return null;
        }
        return control;
    }

    private bool CheckCharInBounds(int i, int length) {
        if (i >= length) {
            Debug.LogWarning("Went out of bounds looking for arguments after control character.");
            return true;
        } else
            return false;
    }

    private void PreCreateControlCommand(string command) {
        string[] cmds = UnitaleUtil.SpecialSplit(':', command);
        string[] args = new string[0];
        if (cmds.Length == 2) {
            args = UnitaleUtil.SpecialSplit(',', cmds[1], true);
            cmds[1] = args[0];
        }
        switch (cmds[0].ToLower()) {
            case "color":       currentColor = ParseUtil.GetColor(cmds[1]);        break;
            case "charspacing": SetHorizontalSpacing(ParseUtil.GetFloat(cmds[1])); break;
            case "linespacing": SetVerticalSpacing(ParseUtil.GetFloat(cmds[1]));   break;

            case "starcolor":
                Color starColor = ParseUtil.GetColor(cmds[1]);
                int indexOfStar = textQueue[currentLine].Text.IndexOf('*'); // HACK oh my god lol
                if (indexOfStar > -1)
                    letterReferences[indexOfStar].color = starColor;
                break;

            case "instant":
                if (args.Length == 0)
                    if (!skipFromPlayer)
                        displayImmediate = true;
                break;

            case "font":
                AudioClip oldClip = letterSound.clip;
                float oldLineThing = Charset.LineSpacing;
                if (GetType() == typeof(LuaTextManager))
                    ((LuaTextManager)this).SetFont(cmds[1]);
                else
                    SetFont(SpriteFontRegistry.Get(cmds[1]));
                letterSound.clip = oldClip;
                //foreach (Letter l in letters)
                //    l.transform.position = new Vector2(l.transform.position.x, l.transform.position.y + (oldLineThing - Charset.LineSpacing));
                //print("currentY from font change (" + textQueue[currentLine].Text + ") = " + currentY + " + " + oldLineThing + " - " + Charset.LineSpacing + " = " + (currentY + (oldLineThing - Charset.LineSpacing)));
                //currentY += (oldLineThing - Charset.LineSpacing);
                break;

            case "effect":
                switch (cmds[1].ToUpper()) {
                    case "NONE":
                        textEffect = null;
                        break;

                    case "TWITCH":
                        if (args.Length > 1)  textEffect = new TwitchEffect(this, ParseUtil.GetFloat(args[1]));
                        else                  textEffect = new TwitchEffect(this);
                        break;

                    case "SHAKE":
                        if (args.Length > 1)  textEffect = new ShakeEffect(this, ParseUtil.GetFloat(args[1]));
                        else                  textEffect = new ShakeEffect(this);
                        break;

                    case "ROTATE":
                        if (args.Length > 1)  textEffect = new RotatingEffect(this, ParseUtil.GetFloat(args[1]));
                        else                  textEffect = new RotatingEffect(this);
                        break;
                }
                break;
        }
    }

    private void InUpdateControlCommand(DynValue command) {
        string[] cmds = UnitaleUtil.SpecialSplit(':', command.String);
        string[] args = new string[0];
        if (cmds.Length >= 2) {
            if (cmds.Length == 3) {
                if (cmds[2] == "skipover" && instantCommand) return;
                else if (cmds[2] == "skiponly" && !instantCommand) return;
            } else if (cmds[1] == "skipover" && instantCommand) return;
            else if (cmds[1] == "skiponly" && !instantCommand) return;
            args = UnitaleUtil.SpecialSplit(',', cmds[1], true);
            cmds[1] = args[0];
        }
        //print("Frame " + GlobalControls.frame + ": Command " + cmds[0].ToLower() + " found for " + gameObject.name);
        switch (cmds[0].ToLower()) {
            case "noskip":
                if (args.Length == 0)      currentSkippable = false;
                else if (args[0] == "off") currentSkippable = true;
                break;

            case "w":
                if (!instantCommand)
                    letterTimer = timePerLetter - (singleFrameTiming * ParseUtil.GetInt(cmds[1]));
                break;

            case "waitall":      timePerLetter = singleFrameTiming * ParseUtil.GetInt(cmds[1]);                  break;
            case "novoice":      letterSound.clip = null;                                                        break;
            case "next":         autoSkipAll = true;                                                             break;
            case "finished":     autoSkipThis = true;                                                            break;
            case "nextthisnow":  autoSkip = true;                                                                break;
            case "noskipatall":  blockSkip = true;                                                               break;
            case "waitfor":      waitingChar = (KeyCode)Enum.Parse(typeof(KeyCode), cmds[1]);                    break;
            case "speed":        letterSpeed = Int32.Parse(args[0]);                                             break;

            case "letters":
                if (!instantCommand)
                    letterOnceValue = Int32.Parse(args[0]);
                break;

            case "voice":
                if (cmds[1].ToLower() == "default")  letterSound.clip = SpriteFontRegistry.Get(SpriteFontRegistry.UI_DEFAULT_NAME).Sound;
                else                                 letterSound.clip = AudioClipRegistry.GetVoice(cmds[1].ToLower());
                break;
                
            case "instant":
                if (args.Length != 0) {
                    switch (args[0].ToLower()) {
                        case "allowcommand": instantCommand = true; break;
                        case "stop":
                            if (!skipFromPlayer)
                                instantCommand = false;
                            break;
                        case "stopall": instantCommand = false; break;
                    }
                }
                break;

            case "func":
                try {
                    if (caller == null)
                        UnitaleUtil.DisplayLuaError("???", "Func called but no script to reference. This is the engine's fault, not yours.");
                    if (args.Length > 1) {
                        DynValue[] argsbis = new DynValue[args.Length - 1];

                        for (int i = 1; i < args.Length; i++) {
                            //The character " is forbidden at the beginning and at the end of the string, as it is the sign of a String.
                            args[i] = args[i].TrimStart('"').TrimEnd('"');
                            Type type = UnitaleUtil.CheckRealType(args[i]);
                            //Boolean
                            if (type == typeof(bool)) {
                                if (args[i].Replace(" ", "") == "true")
                                    argsbis[i - 1] = DynValue.NewBoolean(true);
                                else
                                    argsbis[i - 1] = DynValue.NewBoolean(false);
                            //Number
                            } else if (type == typeof(float)) {
                                args[i] = args[i].Replace(" ", "");
                                float number = CreateNumber(args[i]);
                                argsbis[i - 1] = DynValue.NewNumber(number);
                            //String
                            } else
                                argsbis[i - 1] = DynValue.NewString(args[i]);
                        }
                        caller.Call(args[0], argsbis, true); //ADD TRY
                                                             //caller.Call(args[0], DynValue.NewString(args[1]));
                    } else
                        caller.Call(cmds[1], null, true);
                    if (cmds[1] == "State")
                        wasStated = true;
                } catch (InterpreterException ex) { UnitaleUtil.DisplayLuaError(caller.scriptname, ex.DecoratedMessage); }
                break;

            case "mugshot":
                DynValue temp;
                if (args[0][0] == '{') temp = UnitaleUtil.RebuildTableFromString(args[0]);
                else                   temp = DynValue.NewString(args[0]);

                SetMugshot(temp);
                break;

            case "name":
                string text = textQueue[currentLine].Text;
                string textEnd = text.Substring(0, currentCharacter - 1) + PlayerCharacter.instance.Name + text.Substring(currentCharacter, text.Length - 1);
                textQueue[currentLine].setText(textEnd);
                break;

            case "music":
                if (args[0] == "play")                                                                 Camera.main.GetComponent<AudioSource>().Play();
                else if (args[0] == "pause")                                                           Camera.main.GetComponent<AudioSource>().Pause();
                else if (args[0] == "unpause")                                                         Camera.main.GetComponent<AudioSource>().UnPause();
                else if (args[0] == "stop" || args[0] == "null" || args[0] == "" || args[0] == "nil")  Camera.main.GetComponent<AudioSource>().Stop();
                else {                                                                                 Camera.main.GetComponent<AudioSource>().clip = AudioClipRegistry.GetSound(args[0]);
                                                                                                       Camera.main.GetComponent<AudioSource>().Play();
                }
                break;

            case "sound":
                //In a battle
                if (GameObject.Find("player"))  GameObject.Find("player").GetComponent<AudioSource>().PlayOneShot(AudioClipRegistry.GetSound(args[0]));
                else                            GameObject.Find("Player").GetComponent<AudioSource>().PlayOneShot(AudioClipRegistry.GetSound(args[0]));
                break;

            case "health":
                args[0] = args[0].Replace(" ", "");
                bool killable = false;
                if (args.Length > 1) {
                    args[1] = args[1].Replace(" ", "");
                    if (args[1] == "killable")
                        killable = true;
                }
                float HP = PlayerCharacter.instance.HP, MaxHP = PlayerCharacter.instance.MaxHP, tryHP = 0;
                if (ParseUtil.TestInt(args[0]))
                    tryHP = ParseUtil.GetInt(args[0]);

                if ((args[0].Contains("-") && args[0] != "Max-1") || args[0] == "kill") PlayerController.PlaySound(AudioClipRegistry.GetSound("hurtsound"));
                else if (args.Length > 1) {
                    if (args[1] == "set" && tryHP < HP)                                 PlayerController.PlaySound(AudioClipRegistry.GetSound("hurtsound"));
                    else                                                                PlayerController.PlaySound(AudioClipRegistry.GetSound("healsound"));
                } else                                                                  PlayerController.PlaySound(AudioClipRegistry.GetSound("healsound"));

                if (args[0] == "kill")                   SetHP(0);
                else if (args[0] == "Max-1")             SetHP(MaxHP - 1);
                else if (args[0] == "Max")               SetHP(MaxHP);
                else if (args.Length > 1 && !killable) {
                    if (args[1] == "set")
                        if (tryHP < 1 && !killable)      SetHP(1);
                        else                             SetHP(tryHP);
                } else if (HP + tryHP <= 0 && !killable) SetHP(1);
                else                                     SetHP(HP + tryHP);
                break;

            case "lettereffect":
                letterEffect = args[0];
                if (args.Length == 2)
                    letterIntensity = ParseUtil.GetFloat(args[1]);
                break;
        }
    }

    private void SetHP(float newhp) {
        float HP = 0;
        newhp = Mathf.Round(newhp * Mathf.Pow(10, ControlPanel.instance.MaxDigitsAfterComma)) / Mathf.Pow(10, ControlPanel.instance.MaxDigitsAfterComma);
        if (newhp <= 0) {
            GameOverBehavior gob = GameObject.FindObjectOfType<GameOverBehavior>();
            if (!MusicManager.IsStoppedOrNull(PlayerOverworld.audioKept)) {
                gob.musicBefore = PlayerOverworld.audioKept;
                gob.music = gob.musicBefore.clip;
                gob.musicBefore.Stop();
            } else if (!MusicManager.IsStoppedOrNull(Camera.main.GetComponent<AudioSource>())) {
                gob.musicBefore = Camera.main.GetComponent<AudioSource>();
                gob.music = gob.musicBefore.clip;
                gob.musicBefore.Stop();
            } else {
                gob.musicBefore = null;
                gob.music = null;
            }
            PlayerCharacter.instance.HP = 0;
            this.gameObject.transform.SetParent(null);
            GameObject.DontDestroyOnLoad(this.gameObject);
            RectTransform rt = gameObject.GetComponent<RectTransform>();
            rt.position = new Vector3(rt.position.x, rt.position.y, -1000);
            gob.StartDeath();
            return;
        }
        //HP greater than Max, heal, already more HP than Max
        else if (newhp > PlayerCharacter.instance.MaxHP && newhp > PlayerCharacter.instance.HP && PlayerCharacter.instance.HP > PlayerCharacter.instance.MaxHP)
                                                                                                HP = PlayerCharacter.instance.HP;
        //HP greater than Max, heal
        else if (newhp > PlayerCharacter.instance.MaxHP && newhp > PlayerCharacter.instance.HP) HP = PlayerCharacter.instance.MaxHP;
        else                                                                                    HP = newhp;
        if (HP > ControlPanel.instance.HPLimit)                                                 HP = ControlPanel.instance.HPLimit;
        PlayerCharacter.instance.HP = HP;
    }

    private float CreateNumber(string str) {
        float number = 0, dot = -1;
        int index = 0;
        bool negative = false;
        foreach (char c in str) {
            if (c == '-') {
                negative = true;
                continue;
            } else if (c == ' ') {
                if (dot != -1)
                    dot++;
                continue;
            } else if (c == '.') {
                dot = index;
                index++;
                continue;
            }

            if (dot == -1)
                number = number * 10 + c - 48;
            else
                number += ((float)c - 48) / Mathf.Pow(10, - (dot - index));
            index++;
        }
        if (negative)
            return -number;
        return number;
    }
}