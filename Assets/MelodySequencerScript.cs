using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class MelodySequencerScript : MonoBehaviour
{

    public KMAudio Audio;
    public KMBombInfo Bomb;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    public KMSelectable[] keys;
    public KMSelectable listen;
    public KMSelectable record;
    public KMSelectable move;
    public KMSelectable[] CycleBtns;
    public AudioClip[] Sounds;
    public GameObject Part;
    public GameObject ListenNotes;
    public Material[] KeysUnlit;
    public Material[] KeysLit;

    private int currentPart = 0;
    private int selectedPart = 0;
    private int keysPressed = 0;
    private int partsCreated = 0;

    private string debugText;

    private bool listenActive = false;
    private bool moveActive = false;
    private bool recordActive = false;

    private Color32 standardColor = new Color32(230, 255, 0, 255);
    private Color32 recordColor = new Color32(214, 31, 31, 255);

    private static readonly int[][] parts = new[]
    {
        new[] { 2, 5, 9, 5, 10, 5, 9, 5 },
        new[] { 2, 5, 9, 12, 14, 9, 14, 12 },
        new[] { 17, 14, 17, 21, 22, 17, 22, 21 },
        new[] { 19, 16, 19, 16, 12, 16, 12, 9 },
        new[] { 7, 4, 7, 4, 9, 4, 9, 5 },
        new[] { 10, 5, 10, 7, 12, 7, 12, 9 },
        new[] { 14, 9, 14, 7, 12, 7, 12, 5 },
        new[] { 10, 5, 10, 4, 9, 4, 9, 0 },
    };

    private int[][] moduleParts = new int[8][];

    private static readonly string[] notes = new[] { "C4", "C#4", "D4", "D#4", "E4", "F4", "F#4", "G4", "G#4", "A4", "A#4", "B4", "C5", "C#5", "D5", "D#5", "E5", "F5", "F#5", "G5", "G#5", "A5", "A#5", "B5", };

    void Awake()
    {
        moduleId = moduleIdCounter++;
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i].OnInteract += KeyPressed(i);
        }
        for (int i = 0; i < CycleBtns.Length; i++)
        {
            CycleBtns[i].OnInteract += CycBtnPressed(i);
        }
        listen.OnInteract += delegate () { Listen(); return false; };
        record.OnInteract += delegate () { Record(); return false; };
        move.OnInteract += delegate () { Move(); return false; };
    }

    void Start()
    {
        var partNumber = Enumerable.Range(0, parts.Length).ToList();

        for (int i = 0; i < 4; i++)
        {
            int index = Random.Range(0, partNumber.Count);
            moduleParts[i] = parts[partNumber[index]];
            partNumber.RemoveAt(index);
            for (int j = 0; j < moduleParts[i].Length; j++)
            {
                debugText += notes[moduleParts[i][j]];
                debugText += ", ";
            }
            Debug.LogFormat(@"[Harmony Sequencer #{0}] Shuffled Part {2}: {1}", moduleId, debugText, (i + 1));
            debugText = null;
        }
    }

    private KMSelectable.OnInteractHandler KeyPressed(int keyPressed)
    {
        return delegate ()
        {
            if (listenActive || moveActive || moduleSolved)
                return false;
            if (recordActive)
            {
                RecordInput(keyPressed);
                return false;
            }
            StartCoroutine(GUIUpdate(keyPressed));
            return false;
        };
    }

    private KMSelectable.OnInteractHandler CycBtnPressed(int btnPressed)
    {
        return delegate ()
        {
            if (listenActive || moduleSolved)
                return false;

            if (btnPressed == 0)
                currentPart = (currentPart + 7) % 8;
            else
                currentPart = (currentPart + 1) % 8;

            Part.GetComponent<TextMesh>().text = (currentPart + 1).ToString();
            return false;
        };
    }

    void Listen()
    {
        if (listenActive || moveActive || recordActive || moduleSolved)
            return;
        if (moduleParts[currentPart] != null)
            StartCoroutine(Play());
    }

    void Record()
    {
        if (listenActive || moveActive || moduleSolved)
            return;

        if (recordActive)
        {
            ListenNotes.GetComponent<TextMesh>().color = new Color32(230, 255, 0, 255);
            ListenNotes.SetActive(false);
            recordActive = false;
            return;
        }

        ListenNotes.GetComponent<TextMesh>().text = "Record";
        ListenNotes.GetComponent<TextMesh>().color = new Color32(214, 31, 31, 255);
        ListenNotes.SetActive(true);
        recordActive = true;

    }

    void Move()
    {
        if (listenActive || recordActive || moduleSolved)
            return;

        if (!moveActive)
        {
            moveActive = true;
            selectedPart = currentPart;
            ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.07f, 0.5f, 2);
            ListenNotes.GetComponent<TextMesh>().text = "Selected Part: " + (selectedPart + 1);
            ListenNotes.SetActive(true);
        }
        else
        {
            if (parts[currentPart] == moduleParts[selectedPart])
            {
                int[] modulePartsTemp = moduleParts[currentPart];

                ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.1f, 0.5f, 2);
                ListenNotes.GetComponent<TextMesh>().text = "Well done";

                moduleParts[currentPart] = parts[currentPart];
                moduleParts[selectedPart] = modulePartsTemp;
                StartCoroutine(DisableText());
            }
            else
            {
                GetComponent<KMBombModule>().HandleStrike();
                ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.16f, 0.5f, 2);
                ListenNotes.GetComponent<TextMesh>().text = "Wrong";
                StartCoroutine(DisableText());
            }
            moveActive = false;
        }
    }

    void RecordInput(int keyPressed)
    {
        StartCoroutine(GUIUpdate(keyPressed));

        if (keyPressed == parts[currentPart][keysPressed])
        {
            keysPressed++;
            if (keysPressed == 8)
            {
                ListenNotes.GetComponent<TextMesh>().color = new Color32(230, 255, 0, 255);
                ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.1f, 0.5f, 2);
                ListenNotes.GetComponent<TextMesh>().text = "Well done";
                StartCoroutine(DisableText());
                keysPressed = 0;
                moduleParts[currentPart] = parts[currentPart];
                recordActive = false;
                partsCreated++;
                if (partsCreated == 4)
                    StartCoroutine(Pass());
                
            }
        }
        else
        {
            ListenNotes.GetComponent<TextMesh>().color = new Color32(230, 255, 0, 255);
            ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.16f, 0.5f, 2);
            ListenNotes.GetComponent<TextMesh>().text = "Wrong";
            StartCoroutine(DisableText());
            GetComponent<KMBombModule>().HandleStrike();
            recordActive = false;
        }
    }

    private IEnumerator Pass()
    {
        moduleSolved = true;
        yield return new WaitForSeconds(1f);

        ListenNotes.GetComponent<TextMesh>().color = new Color32(24, 229, 24, 255);
        ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.12f, 0.5f, 2);
        ListenNotes.GetComponent<TextMesh>().text = "Melody";
        ListenNotes.SetActive(true);

        for (int i = 0; i < moduleParts.Length; i++)
        {
            Part.GetComponent<TextMesh>().text = (i + 1).ToString();
            for (int j = 0; j < moduleParts[i].Length; j++)
            {
                Audio.PlaySoundAtTransform(notes[moduleParts[i][j]], transform);
                if (notes[moduleParts[i][j]].Contains("#"))
                {
                    keys[moduleParts[i][j]].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[1];
                    yield return new WaitForSeconds(0.23f);
                    keys[moduleParts[i][j]].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[1];
                }
                else
                {
                    keys[moduleParts[i][j]].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[0];
                    yield return new WaitForSeconds(0.23f);
                    keys[moduleParts[i][j]].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[0];
                }
            }
        }
        GetComponent<KMBombModule>().HandlePass();
        StopAllCoroutines();
    }

    private IEnumerator DisableText()
    {
        yield return new WaitForSeconds(1f);
        ListenNotes.SetActive(false);
        ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.15f, 0.5f, 2);
    }

    private IEnumerator GUIUpdate(int keyPressed)
    {
        Audio.PlaySoundAtTransform(notes[keyPressed], transform);
        if (!recordActive)
        {
            ListenNotes.GetComponent<TextMesh>().text = notes[keyPressed];
            ListenNotes.SetActive(true);
        }

        if (notes[keyPressed].Contains("#"))
        {
            keys[keyPressed].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[1];
            yield return new WaitForSeconds(0.23f);
            keys[keyPressed].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[1];
        }
        else
        {
            keys[keyPressed].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[0];
            yield return new WaitForSeconds(0.23f);
            keys[keyPressed].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[0];
        }

        if (!recordActive)
        {
            yield return new WaitForSeconds(0.5f);
            ListenNotes.SetActive(false);
        }

    }

    private IEnumerator Play()
    {

        listenActive = true;
        for (int i = 0; i < moduleParts[currentPart].Length; i++)
        {
            Audio.PlaySoundAtTransform(notes[moduleParts[currentPart][i]], transform);
            ListenNotes.GetComponent<TextMesh>().text = notes[moduleParts[currentPart][i]];
            ListenNotes.SetActive(true);
            if (notes[moduleParts[currentPart][i]].Contains("#"))
            {
                keys[moduleParts[currentPart][i]].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[1];
                yield return new WaitForSeconds(0.23f);
                keys[moduleParts[currentPart][i]].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[1];
            }
            else
            {
                keys[moduleParts[currentPart][i]].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[0];
                yield return new WaitForSeconds(0.23f);
                keys[moduleParts[currentPart][i]].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[0];
            }
            ListenNotes.SetActive(false);
        }
        listenActive = false;

    }
}