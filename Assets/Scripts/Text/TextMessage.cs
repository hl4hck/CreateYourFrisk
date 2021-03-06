﻿using UnityEngine;
using MoonSharp.Interpreter;

public class TextMessage {
    public TextMessage(string text, bool formatted, bool showImmediate, bool actualText = true, DynValue mugshot = null) {
        Setup(text, formatted, showImmediate, actualText, mugshot);
    }

    public TextMessage(string text, bool formatted, bool showImmediate, DynValue mugshot, bool actualText = true) {
        Setup(text, formatted, showImmediate, actualText, mugshot);
    }
    
    public string Text { get; set; }
    public bool Formatted { get; private set; }
    public bool ShowImmediate { get; private set; }
    public bool ActualText { get; private set; }
    public DynValue Mugshot { get; private set; }

    public void AddToText(string textToAdd) { Text += textToAdd; }

    protected void Setup(string text, bool formatted, bool showImmediate, bool actualText, DynValue mugshot) {
        if (text != "")
            text = unescape(text); // compensate for unity inspector autoescaping control characters
        if (formatted)
            Text = formatText(text);
        else
            Text = text;
        Formatted = formatted;
        ShowImmediate = showImmediate;
        ActualText = actualText;
        //Debug.Log(mugshot);
        Mugshot = mugshot;
    }

    protected void Setup(string text, bool formatted, bool showImmediate) { Setup(text, formatted, showImmediate, true, null); }

    public void setText(string text) { this.Text = text; }

    private string formatText(string text) {
        string newText = "* ", textNew = "";
        if (text == null)
            return text;
        string[] lines = text.Split('\n');
        string[] linesCommands = new string[lines.Length];
        int index = 0;
        if (text.Length != 0)
            for (int i = 0; i < lines.Length; i++) {
                bool needExit = false;
                index = 0;
                if (lines[i].Length != 0)
                    while (lines[i][index] == '[') {
                        if (!(lines[i].Length >= 10 + index && (lines[i].Substring(index, 10) == "[starcolor" || lines[i].Substring(index, 8) == "[letters"))) {
                            if (lines[i][index] == '[') {
                                bool command = false;
                                for (int j = index; j < lines[i].Length; j++)
                                    if (lines[i][j] == ']') {
                                        command = true;
                                        linesCommands[i] += lines[i].Substring(index, j + 1);
                                        lines[i] = lines[i].Substring(index + j + 1, lines[i].Length - index - j - 1);
                                        break;
                                    }
                                if (!command || lines[i].Length == 0) break;
                            }
                        } else
                            while (lines[i][index] != ']')
                                index++;
                                if (index == lines[i].Length) {
                                    needExit = true;
                                    break;
                                }
                        if (needExit)
                            break;
                    }

                if (lines[i].Length != 0)
                    if (lines[i][0] == ' ')
                        lines[i] = lines[i].Substring(1, lines[i].Length - 1);

                if (i == lines.Length - 1) textNew += lines[i];
                else                       textNew += lines[i] + '\n';
            }
        int nCount = 0;
        newText = linesCommands[nCount++] + "* ";
        foreach (char c in textNew) {
            if (c == '\n')      newText += "\n" + linesCommands[nCount ++] + "* ";
            else if (c == '\r') newText += "\n  ";
            else                newText += c;
        }
        return newText;
    }

    public static string unescape(string str) {
        try {
            str = str.Replace("\\n", "\n");
            str = str.Replace("\\r", "\r");
            str = str.Replace("\\t", "\t");
            return str;
        } catch { return str; }
    }
}