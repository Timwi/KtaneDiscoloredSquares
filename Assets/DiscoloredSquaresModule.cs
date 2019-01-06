using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DiscoloredSquares;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Discolored Squares
/// Created by Timwi and EpicToast
/// </summary>
public class DiscoloredSquaresModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;
    public KMColorblindMode ColorblindMode;

    public KMSelectable[] Buttons;
    public Material[] Materials;
    public Material[] MaterialsCB;
    public Material BlackMaterial;
    public Light LightTemplate;

    private Light[] _lights;
    private SquareColor[] _colors;
    private bool _colorblind;
    private static readonly Color[] _lightColors = new[] { Color.white, Color.red, new Color(131f / 255, 131f / 255, 1f), Color.green, Color.yellow, Color.magenta };
    private static readonly SquareColor[] _usefulColors = new[] { SquareColor.Blue, SquareColor.Green, SquareColor.Magenta, SquareColor.Red, SquareColor.Yellow };

    // Contains the (seeded) rules
    private Instruction[] _instructions;
    private int[][] _ordersByStage;
    private SquareColor[] _rememberedColors;
    private SquareColor _neutralColor;
    private int[] _rememberedPositions;
    private int _stage; // 0 = pre-stage 1; 1..4 = stage 1..4
    private List<int> _expectedPresses;
    private int _subprogress;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private Coroutine _activeCoroutine;

    static T[] newArray<T>(params T[] array) { return array; }

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        _colorblind = ColorblindMode.ColorblindModeActive;

        var rnd = RuleSeedable.GetRNG();
        Debug.LogFormat("[Discolored Squares #{0}] Using rule seed: {1}", _moduleId, rnd.Seed);

        var skip = rnd.Next(0, 6);
        for (var i = 0; i < skip; i++)
            rnd.NextDouble();
        _instructions = rnd.ShuffleFisherYates((Instruction[]) Enum.GetValues(typeof(Instruction)));

        var numbers = Enumerable.Range(0, 16).ToArray();
        _ordersByStage = new int[5][];
        for (var stage = 0; stage < 4; stage++)
        {
            rnd.ShuffleFisherYates(numbers);
            _ordersByStage[stage] = numbers.ToArray();
        }

        float scalar = transform.lossyScale.x;
        _lights = new Light[16];
        _colors = new SquareColor[16];

        for (int i = 0; i < 16; i++)
        {
            var j = i;
            Buttons[i].OnInteract += delegate { pressed(j); return false; };
            Buttons[i].GetComponent<MeshRenderer>().material = BlackMaterial;
            var light = _lights[i] = (i == 0 ? LightTemplate : Instantiate(LightTemplate));
            light.name = "Light" + (i + 1);
            light.transform.parent = Buttons[i].transform;
            light.transform.localPosition = new Vector3(0, 0.08f, 0);
            light.transform.localScale = new Vector3(1, 1, 1);
            light.gameObject.SetActive(false);
            light.range = .1f * scalar;
        }

        SetInitialState();
    }

    private void SetInitialState()
    {
        SetAllBlack();

        // Decide which color is the “neutral” color (the remaining four are the “live” ones)
        var colors = _usefulColors.ToArray().Shuffle();
        _rememberedColors = colors.Subarray(0, 4);  // this array will be reordered as the player presses them
        _rememberedPositions = Enumerable.Range(0, 16).ToArray().Shuffle().Subarray(0, 4);  // will be re-populated as the player presses them
        _neutralColor = colors[4];
        _stage = 0;
        _subprogress = 0;

        for (int i = 0; i < 16; i++)
            _colors[i] = _neutralColor;
        for (int i = 0; i < 4; i++)
            _colors[_rememberedPositions[i]] = _rememberedColors[i];

        Debug.LogFormat("[Discolored Squares #{0}] Initial colors are: {1}", _moduleId, _rememberedColors.Select((c, cIx) => string.Format("{0} at {1}", c, coord(_rememberedPositions[cIx]))).Join(", "));
        StartSettingSquareColors(delay: true);
    }

    private static string coord(int ix) { return ((char) ('A' + (ix % 4))) + "" + (ix / 4 + 1); }

    private IEnumerator SetSquareColors(bool delay)
    {
        if (delay)
            yield return new WaitForSeconds(1.75f);
        var sequence = Enumerable.Range(0, 16).Where(ix => _colors[ix] != SquareColor.White).ToList().Shuffle();
        for (int i = 0; i < sequence.Count; i++)
        {
            SetSquareColor(sequence[i]);
            yield return new WaitForSeconds(.03f);
        }
        _activeCoroutine = null;
    }

    void SetSquareColor(int index)
    {
        Buttons[index].GetComponent<MeshRenderer>().material = _colorblind ? MaterialsCB[(int) _colors[index]] ?? Materials[(int) _colors[index]] : Materials[(int) _colors[index]];
        _lights[index].color = _lightColors[(int) _colors[index]];
        _lights[index].gameObject.SetActive(true);
    }

    private void SetBlack(int index)
    {
        Buttons[index].GetComponent<MeshRenderer>().material = BlackMaterial;
        _lights[index].gameObject.SetActive(false);
    }

    private void SetAllBlack()
    {
        for (int i = 0; i < 16; i++)
            SetBlack(i);
    }

    void pressed(int index)
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Buttons[index].transform);
        Buttons[index].AddInteractionPunch();

        if (_stage == 5)    // module is solved
            return;

        if (_stage == 0)
        {
            // Preliminary stage in which the player presses the four “live” colors in any order of their choice
            if (_colors[index] == SquareColor.White)    // ignore re-presses
                return;
            if (_colors[index] == _neutralColor)
            {
                Debug.LogFormat(@"[Discolored Squares #{0}] During the preliminary stage, you pressed a square that wasn’t one of the singular colors. Strike.", _moduleId);
                Module.HandleStrike();
                SetInitialState();
                return;
            }
            playSound(index);
            _rememberedColors[_subprogress] = _colors[index];
            _rememberedPositions[_subprogress] = index;
            _subprogress++;

            // If all colors have been pressed, initialize stage 1
            if (_subprogress == 4)
            {
                Debug.LogFormat(@"[Discolored Squares #{0}] You pressed them in this order: {1}", _moduleId, Enumerable.Range(0, 4).Select(ix => string.Format("{0} ({1})", coord(_rememberedPositions[ix]), _rememberedColors[ix])).Join(", "));
                SetAllBlack();
                SetStage(1);
            }
            else
            {
                _colors[index] = SquareColor.White;
                SetSquareColor(index);
            }
            return;
        }

        if (index != _expectedPresses[_subprogress])
        {
            Debug.LogFormat(@"[Discolored Squares #{0}] Expected {1}, but you pressed {2}. Strike. Module resets.", _moduleId, coord(_expectedPresses[_subprogress]), coord(index));
            Module.HandleStrike();
            SetInitialState();
            return;
        }

        playSound(index);
        _subprogress++;
        _colors[index] = SquareColor.White;
        SetSquareColor(index);
        Debug.LogFormat(@"[Discolored Squares #{0}] {1} was correct.", _moduleId, coord(index));
        if (_subprogress == _expectedPresses.Count)
            SetStage(_stage + 1);
    }

    private void SetStage(int stage)
    {
        _stage = stage;
        _subprogress = 0;
        for (int i = 0; i < 16; i++)
            if (_colors[i] != SquareColor.White)
                SetBlack(i);

        if (stage == 5)
        {
            Module.HandlePass();
            SetAllBlack();
            Debug.LogFormat(@"[Discolored Squares #{0}] Module solved.", _moduleId);
            return;
        }
        Debug.LogFormat(@"[Discolored Squares #{0}] On to stage {1}.", _moduleId, _stage);

        // Put 3–5 of the active color in that many random squares
        var availableSquares = Enumerable.Range(0, 16).Where(ix => stage == 1 || _colors[ix] != SquareColor.White).ToList().Shuffle();
        var take = Math.Min(Rnd.Range(3, 6), availableSquares.Count);
        for (int i = 0; i < take; i++)
            _colors[availableSquares[i]] = _rememberedColors[stage - 1];

        // Fill the rest of the grid with the other colors
        for (int i = take; i < availableSquares.Count; i++)
        {
            var cl = Rnd.Range(1, 5);
            _colors[availableSquares[i]] = (SquareColor) (cl >= (int) _rememberedColors[stage - 1] ? cl + 1 : cl);
        }

        var relevantSquares = availableSquares.Take(take).OrderBy(sq => _ordersByStage[stage - 1][sq]).ToArray();
        Debug.LogFormat("[Discolored Squares #{0}] Stage {1}: {2} squares in the correct order are {3}.", _moduleId, stage, _rememberedColors[stage - 1], relevantSquares.Select(sq => coord(sq)).Join(", "));

        // Process the active squares in the correct order for this stage to compute the intended solution
        _expectedPresses = new List<int>();
        foreach (var activeSquare in relevantSquares)
        {
            if (_expectedPresses.Contains(activeSquare))    // square already became white in this stage
            {
                Debug.LogFormat("[Discolored Squares #{0}] — {1} has already become white. Skip it.", _moduleId, coord(activeSquare));
                continue;
            }
            var solutionSquare = activeSquare;
            do
                solutionSquare = process(solutionSquare, _instructions[_rememberedPositions[stage - 1]]);
            while (_colors[solutionSquare] == SquareColor.White || _expectedPresses.Contains(solutionSquare));
            Debug.LogFormat("[Discolored Squares #{0}] — {1} / {2} translates to {3}", _moduleId, coord(activeSquare), _instructions[_rememberedPositions[stage - 1]], coord(solutionSquare));
            _expectedPresses.Add(solutionSquare);
        }

        StartSettingSquareColors(delay: true);
    }

    private void StartSettingSquareColors(bool delay)
    {
        if (_activeCoroutine != null)
            StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(SetSquareColors(delay));
    }

    private int process(int sq, Instruction instruction)
    {
        int x = sq % 4, y = sq / 4;
        int x2 = x, y2 = y;
        switch (instruction)
        {
            case Instruction.MoveUpLeft: x += 3; y += 3; break;
            case Instruction.MoveUp: y += 3; break;
            case Instruction.MoveUpRight: x++; y += 3; break;
            case Instruction.MoveRight: x++; break;
            case Instruction.MoveDownRight: x++; y++; break;
            case Instruction.MoveDown: y++; break;
            case Instruction.MoveDownLeft: x += 3; y++; break;
            case Instruction.MoveLeft: x += 3; break;
            case Instruction.MirrorHorizontally: x = 3 - x; break;
            case Instruction.MirrorVertically: y = 3 - y; break;
            case Instruction.MirrorDiagonallyA1D4: x = y2; y = x2; break;
            case Instruction.MirrorDiagonallyA4D1: x = 3 - y2; y = 3 - x2; break;
            case Instruction.Rotate90CW: y = x2; x = 3 - y2; break;
            case Instruction.Rotate90CCW: y = 3 - x2; x = y2; break;
            case Instruction.Rotate180: x = 3 - x; y = 3 - y; break;
            default: break;
        }
        return (x % 4) + 4 * (y % 4);
    }

    private void playSound(int index)
    {
        switch (_colors[index])
        {
            case SquareColor.Red:
                Audio.PlaySoundAtTransform("redlight", Buttons[index].transform);
                break;
            case SquareColor.Blue:
                Audio.PlaySoundAtTransform("bluelight", Buttons[index].transform);
                break;
            case SquareColor.Green:
                Audio.PlaySoundAtTransform("greenlight", Buttons[index].transform);
                break;
            case SquareColor.Yellow:
                Audio.PlaySoundAtTransform("yellowlight", Buttons[index].transform);
                break;
            case SquareColor.Magenta:
                Audio.PlaySoundAtTransform("magentalight", Buttons[index].transform);
                break;
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} A1 A2 A3 B3 [specify column as letter, then row as number] | !{0} colorblind";
#pragma warning restore 414

    IEnumerable<KMSelectable> ProcessTwitchCommand(string command)
    {
        if (command.Trim().Equals("colorblind", StringComparison.InvariantCultureIgnoreCase))
        {
            if (!_colorblind)
            {
                _colorblind = true;
                StartCoroutine(SetSquareColors(delay: false));
            }
            return Enumerable.Empty<KMSelectable>();
        }

        var buttons = new List<KMSelectable>();
        foreach (var piece in command.ToLowerInvariant().Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (piece.Length != 2 || piece[0] < 'a' || piece[0] > 'd' || piece[1] < '1' || piece[1] > '4')
                return null;
            buttons.Add(Buttons[(piece[0] - 'a') + 4 * (piece[1] - '1')]);
        }
        return buttons;
    }
}
