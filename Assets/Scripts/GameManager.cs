using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static int RunRundomSeed;
    [SerializeField] private WordsLib _wordsLibrary;
    [SerializeField] private RectTransform _wordZone;
    [SerializeField] private RectTransform _freeZone;
    [SerializeField] private LetterSlot _letterSlotPrefab;
    [SerializeField] private LetterBox _letterBoxPrefab;
    [SerializeField] private float _roundTimer = 30f;
    [SerializeField] private Image _timerFillImage;
    [SerializeField] private TextMeshProUGUI _gameProcessText;
    private CallbacksPreset _callbacksPreset;
    private Queue<string> _wordsQueue = new Queue<string>();
    private LetterSlot[] _letterSlots;
    private LetterBox[] _letterBoxes;
    private Action<int,int,float> _gameOverAction;
    private Coroutine _timerCoroutine;
    private int _failedRounds = 0;
    private DateTime _startRoundTime;
    private float[] _roundsTimers;
    private System.Random _random;
    private string _currentWord = string.Empty;
    public void RestartGame(CallbacksPreset preset, Action<int,int,float> gameOverAction, int seed)
    {
        if(preset == null) return;
        _callbacksPreset = preset;
        _gameOverAction = gameOverAction;
        _random = new System.Random(seed);
        _roundsTimers = new float[_wordsLibrary.Words.Count];
        List<string> words = new(_wordsLibrary.Words);
        while(words.Count > 0)
        {
            int randomIndex = _random.Next(0, words.Count);
            _wordsQueue.Enqueue(words[randomIndex]);
            words.RemoveAt(randomIndex);
        }
        RunRundomSeed = UnityEngine.Random.Range(0, 100);
        SpawnNewWord();
    }

    private void SpawnNewWord()
    {
        if(_wordsQueue.Count == 0)
        {
            _gameOverAction?.Invoke(_wordsLibrary.Words.Count - _failedRounds, _wordsLibrary.Words.Count, _roundsTimers.Sum() / _roundsTimers.Length);
            _gameOverAction = null;
            return;
        }
        ClearWord();
        _currentWord = _wordsQueue.Dequeue();
        int lettersCount = _currentWord.Length;
        _letterSlots = new LetterSlot[lettersCount];
        _letterBoxes = new LetterBox[lettersCount];

        //spawn letter slots
        for (int i = 0; i < lettersCount; i++)
        {
            LetterSlot letterSlot = Instantiate(_letterSlotPrefab, _wordZone);
            letterSlot.gameObject.name = $"LetterSlot_{i}";
            letterSlot.Setup(i);
            _letterSlots[i] = letterSlot;
        }
        //spawn letter boxes
        for (int i = 0; i < lettersCount; i++)
        {
            LetterBox letterBox = Instantiate(_letterBoxPrefab, _freeZone);
            letterBox.gameObject.name = $"LetterBox_{i}";
            letterBox.Setup(_currentWord[i].ToString(), _callbacksPreset);
            _letterBoxes[i] = letterBox;
            _letterBoxes[i].transform.position = _freeZone.position + Vector3.left * lettersCount/2 * 150f + Vector3.right * i * 180f;
            letterBox.OnEndDragAction += OnLetterBoxEndDrag;
        }
        for (int i = 0; i < lettersCount; i++)
        {
            var pos = _letterBoxes[i].transform.position;
            int rID = _random.Next(0, lettersCount);
            _letterBoxes[i].transform.position = _letterBoxes[rID].transform.position;
            _letterBoxes[rID].transform.position = pos;
        }
        if(_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _roundsTimers[_wordsLibrary.Words.Count - _wordsQueue.Count-1] = (float)(DateTime.Now - _startRoundTime).TotalSeconds;
        }
        _startRoundTime = DateTime.Now;
        _timerCoroutine = StartCoroutine(TimerRoutine());

        _gameProcessText.text = string.Format("{0}/{1}", Mathf.Clamp(_wordsLibrary.Words.Count - _wordsQueue.Count, 0, int.MaxValue), _wordsLibrary.Words.Count);
    }
    private void CheckIfWordIsComplete()
    {
        string word = string.Empty;
        for (int i = 0; i < _letterSlots.Length; i++)
        {
            if (_letterSlots[i].StoredLetterBox == null) return;
            word += _letterSlots[i].StoredLetterBox.Letter;
        }
        if(word != _currentWord) return;
        //word is complete
        SpawnNewWord();
    }
    private void ClearWord()
    {
        if(_letterSlots == null || _letterBoxes == null) return;
        for (int i = 0; i < _letterBoxes.Length; i++)
        {
            Destroy(_letterBoxes[i].gameObject);
        }
        for (int i = 0; i < _letterSlots.Length; i++)
        {
            Destroy(_letterSlots[i].gameObject);
        }
    }
    private void OnLetterBoxEndDrag(LetterBox letterBox)
    {
        if(letterBox.IsInWord)
        {
            if(RectTransformUtility.RectangleContainsScreenPoint(_wordZone, letterBox.transform.position))
            {
                //try swap between slots
                if(!TrySwapSlot(letterBox)) letterBox.FlyBack();
                else CheckIfWordIsComplete();
            }
            else 
            {
                //remove from word
                _letterSlots[letterBox.OwnerSlotID].ClearSlot();
                letterBox.OnRemovedFromWord();
                letterBox.transform.SetParent(_freeZone);
            }
        }
        else
        {
            if(!TryEnqueueSlot(letterBox)) letterBox.FlyBack();
            else CheckIfWordIsComplete();
        }
    }
    private bool TrySwapSlot(LetterBox letterBox)
    {
        int previousSlotID = letterBox.OwnerSlotID;
        for (int i = 0; i < _letterSlots.Length; i++)
        {
            if (_letterSlots[i].IsInRect(letterBox.transform.position))
            {
                if (_letterSlots[i].TryEnqueueLetterBox(letterBox))
                {
                    _letterSlots[previousSlotID].ClearSlot();
                    return true;
                }
            }
        }
        return false;
    }
    private bool TryEnqueueSlot(LetterBox letterBox)
    {
        for (int i = 0; i < _letterSlots.Length; i++)
        {
            if (_letterSlots[i].IsInRect(letterBox.transform.position))
            {
                if (_letterSlots[i].TryEnqueueLetterBox(letterBox)) return true;
            }
        }
        return false;
    }
    private IEnumerator TimerRoutine()
    {
        float timer = _roundTimer;
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            _timerFillImage.fillAmount = 1f - timer / _roundTimer;
            yield return null;
        }
        _failedRounds++;
        _roundsTimers[_wordsLibrary.Words.Count - _wordsQueue.Count-1] = (float)(DateTime.Now - _startRoundTime).TotalSeconds;
        SpawnNewWord();
    }
}
