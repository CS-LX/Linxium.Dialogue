using System;
using DG.Tweening;
using Linxium.ExtraComponents;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Linxium.Dialogue.Implementation {
    public class GameDialogue : AdvancedMonoSingleton<GameDialogue> {
        [Header("UI References")]
        public CanvasGroup dialogueCanvasGroup;
        public Image backgroundImage;
        public Image characterImage;
        public TMP_Text nameText;
        public TMP_Text contentText;
        public Button skipButton;
        public Button autoButton;
        public GameObject optionsParent;
        public GameObject optionButtonPrefab;

        [Header("Typing Settings")] public float typeInterval = 0.05f;

        [Header("Auto Settings")] public float autoNextDelay = 2f; // 自动模式下一句间隔时间

        [Header("Animation Settings")] public float fadeDuration = 0.3f;
        public float optionPopDelay = 0.05f; // 每个选项弹出间隔
        public string autoOnText = "自动: 开";
        public string autoOffText = "自动: 关";

        public string AutoOnText {
            get => autoOnText;
            set {
                autoOnText = value;
                UpdateUI();
            }
        }

        public string AutoOffText {
            get => autoOffText;
            set {
                autoOffText = value;
                UpdateUI();
            }
        }

        [Header("Others")]
        public TextAsset startStory;
        public TMP_FontAsset textFont;

        public UnityEvent OnDialogueStart = new();
        public UnityEvent OnDialogueEnd = new();
        public UnityEvent OnDialogueEndInner = new();

        DialogueRunner dialogueRunner;

        protected override void OnAwake() {
            dialogueRunner = new DialogueRunner {
                dialogueCanvasGroup = dialogueCanvasGroup,
                backgroundImage = backgroundImage,
                characterImage = characterImage,
                nameText = nameText,
                contentText = contentText,
                skipButton = skipButton,
                autoButton = autoButton,
                optionsParent = optionsParent,
                optionButtonPrefab = optionButtonPrefab,
                typeInterval = typeInterval,
                autoNextDelay = autoNextDelay,
                fadeDuration = fadeDuration,
                optionPopDelay = optionPopDelay,
                startStory = startStory,
                OnDialogueStart = OnDialogueStart,
                OnDialogueEnd = OnDialogueEnd,
                OnDialogueEndInner = OnDialogueEndInner
            };
            dialogueRunner.Awake();
            if (textFont) {
                foreach (TMP_Text tmpText in GetComponentsInChildren<TMP_Text>()) {
                    tmpText.font = textFont;
                }
                dialogueRunner.OnChoice.AddListener((_, obj) => obj.GetComponentInChildren<TextMeshProUGUI>().font = textFont);
            }
            dialogueRunner.OnToggleAuto.AddListener(UpdateUI);
            UpdateUI();
        }

        protected override void OnStart() {
            base.OnStart();
            dialogueRunner.Start();
        }

        protected override void OnDispose() {
            base.OnDispose();
            dialogueRunner.Dispose();
        }

        public virtual void StartDialogue(TextAsset inkAsset, Action onDialogueEnd = null) {
            dialogueRunner.StartDialogue(inkAsset, onDialogueEnd);
        }

        public void UpdateUI() {
            autoButton.GetComponentInChildren<TMP_Text>().text = dialogueRunner.IsAuto ? autoOnText : autoOffText;
        }
    }
}