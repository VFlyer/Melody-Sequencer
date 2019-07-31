using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

public class MelodySequencerScript : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMRuleSeedable RuleSeedable;

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

    private bool listenActive = false;
    private bool moveActive = false;
    private bool recordActive = false;

    private static readonly int[][] seed1parts = new[]
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

    private int[][] parts;
    private int[][] moduleParts = new int[8][];
    private List<int> givenParts = new List<int>();

    private static readonly string[] noteNames = new[] { "C4", "C#4", "D4", "D#4", "E4", "F4", "F#4", "G4", "G#4", "A4", "A#4", "B4", "C5", "C#5", "D5", "D#5", "E5", "F5", "F#5", "G5", "G#5", "A5", "A#5", "B5", };

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

        var rnd = RuleSeedable.GetRNG();
        Debug.LogFormat(@"[Melody Sequencer #{0}] Using rule seed: {1}", moduleId, rnd.Seed);
        if (rnd.Seed == 1)
            parts = seed1parts;
        else
        {
            // Decide on the key of the first part. The rest of the parts are in specific keys relative to each previous one
            var keys = new List<int> { rnd.Next(0, 12) };
            for (int partIx = 1; partIx < 8; partIx++)
            {
                var eligibleKeys = new[] { 5, 7, -5, -7 }.Select(jump => keys[partIx - 1] + jump).Where(key => key >= 0 && key < 12).ToList();
                keys.Add(eligibleKeys[rnd.Next(0, eligibleKeys.Count)]);
            }

            // Generate a new melody at random!
            parts = new int[8][];
            for (int partIx = 0; partIx < 8; partIx++)
            {
                var notes = new[] { 0, 2, 4, 5, 7, 9, 11, 12, 14, 16, 17, 19, 21, 23 }.Select(i => (i + keys[partIx]) % 24).ToArray();
                var majorNotes = new[] { 0, 4, 7, 12, 16, 19 }.Select(i => (i + keys[partIx]) % 24).ToArray();

                parts[partIx] = new int[8];

                // Make sure that we do not accidentally generate two identical parts
                do
                {
                    for (int note = 0; note < 8; note++)
                    {
                        var eligibleNotes = (note % 2 == 0 ? majorNotes : notes).ToList();
                        if (note > 0)
                            eligibleNotes.RemoveAll(n => Mathf.Abs(n - parts[partIx][note - 1]) >= 7);
                        else if (partIx > 0)
                            eligibleNotes.RemoveAll(n => Mathf.Abs(n - parts[partIx - 1].Last()) >= 7);
                        if (note > 1 && parts[partIx][note - 1] == parts[partIx][note - 2])
                            eligibleNotes.Remove(parts[partIx][note - 1]);
                        parts[partIx][note] = eligibleNotes[rnd.Next(0, eligibleNotes.Count)];
                    }
                }
                while (Enumerable.Range(0, partIx).Any(p => parts[p].SequenceEqual(parts[partIx])));

                Debug.LogFormat(@"[Melody Sequencer #{0}] Solution part {1}: {2}", moduleId, partIx + 1, string.Join(", ", parts[partIx].Select(note => noteNames[note]).ToArray()));
            }
        }
    }

    void Start()
    {
        var partNumbers = Enumerable.Range(0, parts.Length).ToList();
        var slotNumbers = Enumerable.Range(0, parts.Length).ToList();

        for (int i = 0; i < 4; i++)
        {
            int partIx = Random.Range(0, partNumbers.Count);
            int slotIx = Random.Range(0, slotNumbers.Count);
            moduleParts[slotNumbers[slotIx]] = parts[partNumbers[partIx]];
            givenParts.Add(partNumbers[partIx]);
            Debug.LogFormat(@"[Melody Sequencer #{0}] Slot {1} contains part {2}: {3}", moduleId, slotNumbers[slotIx] + 1, partNumbers[partIx] + 1, string.Join(", ", parts[partNumbers[partIx]].Select(note => noteNames[note]).ToArray()));
            partNumbers.RemoveAt(partIx);
            slotNumbers.RemoveAt(slotIx);
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

        if (givenParts.Contains(currentPart))
        {
            Debug.LogFormat(@"[Melody Sequencer #{0}] You tried to record part #{1} but that part is already given. Strike!", moduleId, currentPart + 1);
            ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.16f, 0.5f, 2);
            ListenNotes.GetComponent<TextMesh>().text = "Wrong";
            ListenNotes.SetActive(true);
            StartCoroutine(DisableText());
            GetComponent<KMBombModule>().HandleStrike();
            return;
        }

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
                Debug.LogFormat(@"[Melody Sequencer #{0}] You successfully swapped slot {1} with slot {2}.", moduleId, selectedPart + 1, currentPart + 1);

                moduleParts[currentPart] = parts[currentPart];
                moduleParts[selectedPart] = modulePartsTemp;
                StartCoroutine(DisableText());
            }
            else
            {
                GetComponent<KMBombModule>().HandleStrike();
                ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.16f, 0.5f, 2);
                ListenNotes.GetComponent<TextMesh>().text = "Wrong";
                Debug.LogFormat(@"[Melody Sequencer #{0}] You tried to swap slot {1} with slot {2} — strike!", moduleId, selectedPart + 1, currentPart + 1);
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
            Debug.LogFormat(@"[Melody Sequencer #{0}] For part {1}, you entered {2} but I expected {3}", moduleId, currentPart + 1,
                string.Join(", ", parts[currentPart].Take(keysPressed).Concat(new[] { keyPressed }).Select(note => noteNames[note]).ToArray()),
                string.Join(", ", parts[currentPart].Take(keysPressed + 1).Select(note => noteNames[note]).ToArray()));

            ListenNotes.GetComponent<TextMesh>().color = new Color32(230, 255, 0, 255);
            ListenNotes.GetComponent<Transform>().localScale = new Vector3(0.16f, 0.5f, 2);
            ListenNotes.GetComponent<TextMesh>().text = "Wrong";
            StartCoroutine(DisableText());
            GetComponent<KMBombModule>().HandleStrike();
            keysPressed = 0;
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

        for (int i = 0; i < parts.Length; i++)
        {
            Part.GetComponent<TextMesh>().text = (i + 1).ToString();
            for (int j = 0; j < parts[i].Length; j++)
            {
                Audio.PlaySoundAtTransform(noteNames[parts[i][j]], transform);
                if (noteNames[parts[i][j]].Contains("#"))
                {
                    keys[parts[i][j]].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[1];
                    yield return new WaitForSeconds(0.23f);
                    keys[parts[i][j]].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[1];
                }
                else
                {
                    keys[parts[i][j]].GetComponent<MeshRenderer>().sharedMaterial = KeysUnlit[0];
                    yield return new WaitForSeconds(0.23f);
                    keys[parts[i][j]].GetComponent<MeshRenderer>().sharedMaterial = KeysLit[0];
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
        Audio.PlaySoundAtTransform(noteNames[keyPressed], transform);
        if (!recordActive)
        {
            ListenNotes.GetComponent<TextMesh>().text = noteNames[keyPressed];
            ListenNotes.SetActive(true);
        }

        if (noteNames[keyPressed].Contains("#"))
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
            Audio.PlaySoundAtTransform(noteNames[moduleParts[currentPart][i]], transform);
            ListenNotes.GetComponent<TextMesh>().text = noteNames[moduleParts[currentPart][i]];
            ListenNotes.SetActive(true);
            if (noteNames[moduleParts[currentPart][i]].Contains("#"))
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

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} slot 4 [select slot 4] | !{0} play 4 [select slot 4 and play it] | !{0} move to 4 [move the current selected slot to slot 4] | !{0} record C# D# F [press record and play these notes] | !{0} play C# D# F [just play these notes]";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        Match m;
        if ((m = Regex.Match(command, @"^\s*(slot|select)\s+(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            var slotNumber = int.Parse(m.Groups[2].Value);
            if (slotNumber < 1 || slotNumber > 8)
                yield break;
            yield return null;
            yield return Enumerable.Repeat(CycleBtns[1], ((slotNumber - 1) - currentPart + 8) % 8).ToArray();
        }

        if ((m = Regex.Match(command, @"^\s*(play|listen +to)\s+(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            var slotNumber = int.Parse(m.Groups[2].Value);
            if (slotNumber < 1 || slotNumber > 8)
                yield break;
            yield return null;
            yield return Enumerable.Repeat(CycleBtns[1], ((slotNumber - 1) - currentPart + 8) % 8).Concat(new[] { listen }).ToArray();
        }

        if ((m = Regex.Match(command, @"^\s*(move|yellow|move +to)\s+(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            var slotNumber = int.Parse(m.Groups[2].Value);
            if (slotNumber < 1 || slotNumber > 8)
                yield break;
            yield return null;
            yield return new[] { move }.Concat(Enumerable.Repeat(CycleBtns[1], ((slotNumber - 1) - currentPart + 8) % 8)).Concat(new[] { move }).ToArray();
        }

        if ((m = Regex.Match(command, @"^\s*(record|submit|input|enter|red|play|press)\s+([ABCDEFG#♯45 ,;]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            var sequence = m.Groups[2].Value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var keysToPress = new List<KMSelectable>();
            if (m.Groups[1].Value != "press" && m.Groups[1].Value != "play")
                keysToPress.Add(record);
            for (int i = 0; i < sequence.Length; i++)
            {
                var ix = Array.IndexOf(noteNames, sequence[i].ToUpperInvariant().Replace("♯", "#"));
                if (ix == -1)
                    yield break;
                keysToPress.Add(keys[ix]);
            }
            yield return null;
            foreach (var key in keysToPress)
            {
                yield return new[] { key };
                yield return new WaitForSeconds(.13f);
            }
        }
    }
}