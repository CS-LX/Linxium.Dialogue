using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Ink.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Linxium.Dialogue {
    public class DialogueRunner : IDisposable {
        [Header("UI References")] public CanvasGroup dialogueCanvasGroup;
        public Image backgroundImage;
        public Image characterImage;
        public TMP_Text nameText;
        public TMP_Text contentText;
        public Button skipButton;
        public Button autoButton;
        public GameObject optionsParent;
        public GameObject optionButtonPrefab;

        [Header("Typing Settings")]
        public float typeInterval = 0.05f;

        [Header("Auto Settings")]
        public float autoNextDelay = 2f; // 自动模式下一句间隔时间

        [Header("Animation Settings")]
        public float fadeDuration = 0.3f;
        public float optionPopDelay = 0.05f; // 每个选项弹出间隔
        public string autoOnText = "自动: 开";
        public string autoOffText = "自动: 关";

        [Header("Others")]
        public TextAsset startStory;

        Story currentStory;
        bool isTyping;
        bool autoLine;
        string currentLine;
        Tween fadeTween;
        CancellationTokenSource typeSentenceCTS;

        public UnityEvent OnDialogueStart = new();
        public UnityEvent OnDialogueEnd = new();
        public UnityEvent OnDialogueEndInner = new();
        public UnityEvent<Story> OnBindExternalFunctions = new();
        public UnityEvent<Story> OnUnbindExternalFunctions = new();
        public UnityEvent<Choice, GameObject> OnChoice = new();
        public bool HasDialogue => currentStory != null;

        public void Awake() {
            if (dialogueCanvasGroup != null) {
                dialogueCanvasGroup.alpha = 0f;
                dialogueCanvasGroup.interactable = false;
                dialogueCanvasGroup.blocksRaycasts = false;
            }
            skipButton.onClick.AddListener(SkipDialogue);
            autoButton.onClick.AddListener(() => {
                    autoLine = !autoLine;
                    var txt = autoButton.GetComponentInChildren<TMP_Text>();
                    txt.text = autoLine ? autoOnText : autoOffText;
                    txt.DOFade(1f, 0.25f);

                    // 如果此时没有在打字、也没有选项、且对话还在继续 → 立即自动继续
                    if (autoLine
                        && !isTyping
                        && !HasChoices()
                        && currentStory != null
                        && currentStory.canContinue)
                        ContinueStory();
                }
            );
            backgroundImage.GetComponent<Button>().onClick.AddListener(SkipSentence);
        }

        public void Start() {
            if (startStory) StartDialogue(startStory);
        }

        public void Dispose() {
            typeSentenceCTS?.Cancel();
            typeSentenceCTS?.Dispose();
        }

        public void StartDialogue(TextAsset inkFile, Action onDialogueEnd = null) {
            // 如果当前有正在进行的对话，先停止它
            if (currentStory != null) {
                EndDialogue();
            }
            currentStory = new Story(inkFile.text);
            if (onDialogueEnd != null) OnDialogueEndInner.AddListener(onDialogueEnd.Invoke);
            BindExternalFunctions();
            dialogueCanvasGroup.interactable = true;
            dialogueCanvasGroup.blocksRaycasts = true;

            // 使用 DOTween 淡入动画
            fadeTween?.Kill();
            fadeTween = dialogueCanvasGroup.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad).SetUpdate(true);
            ContinueStory();
            OnDialogueStart.Invoke();
        }

        public void SkipSentence() {
            if (isTyping)
                CompleteTyping();
            else if (!HasChoices()) ContinueStory();
        }

        public void SkipDialogue() {
            if (isTyping)
                CompleteTyping();
            else
                EndDialogue();
        }

        void BindExternalFunctions() {
            OnBindExternalFunctions.Invoke(currentStory);
        }

        void UnbindExternalFunctions() {
            OnUnbindExternalFunctions.Invoke(currentStory);
        }

        void ContinueStory() {
            if (currentStory.canContinue) {
                currentLine = currentStory.Continue().Trim();
                ShowLine(currentLine);
                ShowAppear(Appear.Create(currentStory.currentTags));
            }
            else if (HasChoices()) {
                DisplayChoices();
            }
            else {
                EndDialogue();
            }
        }

        void ShowLine(string line) {
            contentText.text = line;
            contentText.maxVisibleCharacters = 0;
            typeSentenceCTS?.Cancel();
            typeSentenceCTS?.Dispose();
            typeSentenceCTS = new CancellationTokenSource();
            TypeSentenceUnscaled(line.Length,  typeSentenceCTS.Token).Forget();
        }

        void ShowAppear(Appear appear) {
            nameText.text = appear.character.HasContent(out string content) ? content : string.Empty;
            characterImage.sprite = appear.tachie;
            characterImage.color = new Color(characterImage.color.r, characterImage.color.g, characterImage.color.b, appear.tachie ? 1f : 0f);
            backgroundImage.sprite = appear.background;
            backgroundImage.color = new Color(backgroundImage.color.r, backgroundImage.color.g, backgroundImage.color.b, appear.background ? 1f : 0f);
        }

        async UniTask TypeSentenceUnscaled(int totalChars, CancellationToken token) {
            try {
                isTyping = true;
                int visibleCount = 0;

                // 打字过程（不受 TimeScale 影响）
                while (visibleCount < totalChars) {
                    token.ThrowIfCancellationRequested();
                    visibleCount++;
                    contentText.maxVisibleCharacters = visibleCount;
                    await UniTask.WaitForSeconds(typeInterval, true, cancellationToken: token);
                }
                isTyping = false;

                // 如果有选项，优先展示
                if (HasChoices()) {
                    DisplayChoices();
                }
                else if (autoLine) {
                    await UniTask.WaitForSeconds(autoNextDelay, true, cancellationToken: token);

                    // 等待期间用户可能关掉 auto
                    if (autoLine && !HasChoices()) ContinueStory();
                }
            }
            catch (OperationCanceledException) { }
        }

        void CompleteTyping() {
            typeSentenceCTS?.Cancel();
            typeSentenceCTS?.Dispose();
            typeSentenceCTS = null;
            contentText.maxVisibleCharacters = contentText.text.Length;
            isTyping = false;
            if (HasChoices()) DisplayChoices();
        }

        void DisplayChoices() {
            ClearChoices();
            var choices = currentStory.currentChoices;
            for (int i = 0; i < choices.Count; i++) {
                var localChoice = choices[i]; // 修复闭包问题
                var btnObj = Object.Instantiate(optionButtonPrefab, optionsParent.transform);
                var btnRect = btnObj.GetComponent<RectTransform>();
                var btnCanvasGroup = btnObj.GetComponent<CanvasGroup>();
                if (btnCanvasGroup == null) btnCanvasGroup = btnObj.AddComponent<CanvasGroup>(); // 保证有CanvasGroup
                var btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                btnText.text = localChoice.text;
                OnChoice.Invoke(localChoice, btnObj);

                // 初始化为隐藏状态
                btnRect.localScale = Vector3.one * 0.95f;
                btnCanvasGroup.alpha = 0f;

                // 添加点击事件
                btnObj.GetComponent<Button>()
                .onClick.AddListener(() => {
                        currentStory.ChooseChoiceIndex(localChoice.index);
                        ClearChoices();
                        ContinueStory();
                    }
                );

                // 动画部分：渐显 + 弹性缩放
                float delay = i * optionPopDelay;
                DOTween.Sequence().Join(btnCanvasGroup.DOFade(1, 0.25f).SetEase(Ease.OutCubic)).Join(btnRect.DOScale(1f, 0.25f).SetEase(Ease.OutBack)).SetDelay(delay).SetUpdate(true); // 保证在暂停时仍可播放UI动画
            }
        }

        bool HasChoices() {
            return currentStory != null && currentStory.currentChoices.Count > 0;
        }

        void ClearChoices() {
            foreach (Transform child in optionsParent.transform) Object.Destroy(child.gameObject);
        }

        void EndDialogue() {
            fadeTween?.Kill();

            // DOTween淡出动画
            fadeTween = dialogueCanvasGroup.DOFade(0f, fadeDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(true)
            .OnComplete(() => {
                    dialogueCanvasGroup.interactable = false;
                    dialogueCanvasGroup.blocksRaycasts = false;
                    ClearChoices();
                    UnbindExternalFunctions();
                    currentStory = null;
                    var temp = OnDialogueEndInner;
                    OnDialogueEndInner = new UnityEvent();
                    temp.Invoke();
                    OnDialogueEnd.Invoke();
                }
            );
        }
    }
}