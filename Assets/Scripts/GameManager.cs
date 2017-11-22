﻿/*
 * Copyright (c) 2016 Razeware LLC
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour {
    public static GameManager instance;
    public string playerName;
    public GameObject buttonPrefab;
    private string selectedLevel;
    void Awake () {
        if (instance == null) {
            instance = this;
        } else if (instance != this) {
            Destroy (gameObject);
        }
        DontDestroyOnLoad (gameObject);
    }

    /// <summary>
    /// Start is called on the frame when a script is enabled just before
    /// any of the Update methods is called the first time.
    /// </summary>
    void Start () {
        SceneManager.sceneLoaded += OnSceneLoaded;
        DiscoverLevels ();
    }

    /// <summary>
    /// Update is called every frame, if the MonoBehaviour is enabled.
    /// </summary>
    void Update () {
        if (Input.GetKeyDown (KeyCode.Escape)) {
            SceneManager.LoadScene ("Menu");
        }
    }

    public void RestartLevel (float delay) {
        StartCoroutine (RestartLevelDelay (delay));
    }

    private IEnumerator RestartLevelDelay (float delay) {
        yield return new WaitForSeconds (delay);
        SceneManager.LoadScene ("Game");
    }
    
    public List<PlayerTimeEntry> LoadPreviousTimes () {
        // 1
        try {
            var levelName = Path.GetFileName (selectedLevel);
            var scoresFile = Application.persistentDataPath + "/" + playerName + "_" + levelName + "_times.dat";
            using (var stream = File.Open (scoresFile, FileMode.Open)) {
                var bin = new BinaryFormatter ();
                var times = (List<PlayerTimeEntry>) bin.Deserialize (stream);
                return times;
            }
        }
        // 2
        catch (IOException ex) {
            Debug.LogWarning ("Couldn’t load previous times for: " + playerName + ". Exception: " + ex.Message);
            return new List<PlayerTimeEntry> ();
        }
    }
    public void SaveTime (decimal time) {
        // 3
        var times = LoadPreviousTimes ();
        // 4
        var newTime = new PlayerTimeEntry ();
        newTime.entryDate = DateTime.Now;
        newTime.time = time;
        // 5
        var bFormatter = new BinaryFormatter ();
        var levelName = Path.GetFileName (selectedLevel);
        var filePath = Application.persistentDataPath + "/" + playerName + "_" + levelName + "_times.dat";
        using (var file = File.Open (filePath, FileMode.Create)) {
            times.Add (newTime);
            bFormatter.Serialize (file, times);
        }
    }

    public void DisplayPreviousTimes () {
        var times = LoadPreviousTimes ();
        var levelName = Path.GetFileName (selectedLevel);
        if (levelName != null) {
            levelName = levelName.Replace (".json", "");
        }
        var topThree = times.OrderBy (time => time.time).Take (3);
        var timesLabel = GameObject.Find ("PreviousTimes")
            .GetComponent<Text> ();

        timesLabel.text = levelName + "\n";
        timesLabel.text += "BEST TIMES \n";
        foreach (var time in topThree) {
            timesLabel.text += time.entryDate.ToShortDateString () +
                ": " + time.time + "\n";
        }
    }

    private void OnSceneLoaded (Scene scene, LoadSceneMode loadsceneMode) {
        if (!string.IsNullOrEmpty (selectedLevel) && scene.name == "Game") {
            Debug.Log ("Loading level content for: " + selectedLevel);
            LoadLevelContent ();
            DisplayPreviousTimes ();
        }
        if (scene.name == "Menu") DiscoverLevels ();
    }

    private void SetLevelName (string levelFilePath) {
        selectedLevel = levelFilePath;
        SceneManager.LoadScene ("Game");
    }

    private void DiscoverLevels () {
        var levelPanelRectTransform = GameObject.Find ("LevelItemsPanel").GetComponent<RectTransform> ();
        var levelFiles = Directory.GetFiles (Application.dataPath, "*.json");

        var yOffset = 0f;
        for (var i = 0; i < levelFiles.Length; i++) {
            if (i == 0) {
                yOffset = -30f;
            } else {
                yOffset -= 65f;
            }
            var levelFile = levelFiles[i];
            var levelName = Path.GetFileName (levelFile);

            var levelButtonObj = (GameObject) Instantiate (buttonPrefab, Vector2.zero, Quaternion.identity);
            // 2
            var levelButtonRectTransform = levelButtonObj.GetComponent<RectTransform> ();
            levelButtonRectTransform.SetParent (levelPanelRectTransform, true);
            // 3
            levelButtonRectTransform.anchoredPosition = new Vector2 (212.5f, yOffset);
            // 4
            var levelButtonText = levelButtonObj.transform.GetChild (0).GetComponent<Text> ();
            levelButtonText.text = levelName;

            var levelButton = levelButtonObj.GetComponent<Button> ();
            levelButton.onClick.AddListener (delegate {
                SetLevelName (levelFile);
            });
            levelPanelRectTransform.sizeDelta = new Vector2 (levelPanelRectTransform.sizeDelta.x, 60f * i);
        }

        levelPanelRectTransform.offsetMax = new Vector2 (levelPanelRectTransform.offsetMax.x, 0f);
    }

    private void LoadLevelContent () {
        var existingLevelRoot = GameObject.Find ("Level");
        Destroy (existingLevelRoot);
        var levelRoot = new GameObject ("Level");

        // 1
        var levelFileJsonContent = File.ReadAllText (selectedLevel);
        var levelData = JsonUtility.FromJson<LevelDataRepresentation> (
            levelFileJsonContent);
        // 2
        foreach (var li in levelData.levelItems) {
            // 3
            var pieceResource =
                Resources.Load ("Prefabs/" + li.prefabName);
            if (pieceResource == null) {
                Debug.LogError ("Cannot find resource: " + li.prefabName);
            }
            // 4
            var piece = (GameObject) Instantiate (pieceResource,
                li.position, Quaternion.identity);
            var pieceSprite = piece.GetComponent<SpriteRenderer> ();
            if (pieceSprite != null) {
                pieceSprite.sortingOrder = li.spriteOrder;
                pieceSprite.sortingLayerName = li.spriteLayer;
                pieceSprite.color = li.spriteColor;
            }
            // 5
            piece.transform.parent = levelRoot.transform;
            piece.transform.position = li.position;
            piece.transform.rotation = Quaternion.Euler (
                li.rotation.x, li.rotation.y, li.rotation.z);
            piece.transform.localScale = li.scale;
        }

        var SoyBoy = GameObject.Find ("SoyBoy");
        SoyBoy.transform.position = levelData.playerStartPosition;
        Camera.main.transform.position = new Vector3 (
            SoyBoy.transform.position.x, SoyBoy.transform.position.y,
            Camera.main.transform.position.z);

        // 1
        var camSettings = FindObjectOfType<CameraLerpToTransform> ();
        // 2
        if (camSettings != null) {
            camSettings.cameraZDepth = levelData.cameraSettings.cameraZDepth;
            camSettings.camTarget = GameObject.Find (
                levelData.cameraSettings.cameraTrackTarget).transform;
            camSettings.maxX = levelData.cameraSettings.maxX;
            camSettings.maxY = levelData.cameraSettings.maxY;
            camSettings.minX = levelData.cameraSettings.minX;
            camSettings.minY = levelData.cameraSettings.minY;
            camSettings.trackingSpeed = levelData.cameraSettings.trackingSpeed;
        }
    }
}