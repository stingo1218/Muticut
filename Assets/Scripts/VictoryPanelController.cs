using UnityEngine;
using UnityEngine.UI;

public class VictoryPanelController : MonoBehaviour
{
    [SerializeField] private Button continueButton;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool hideOnAwake = true;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (continueButton == null)
            continueButton = GetComponentInChildren<Button>(true);

        if (continueButton != null)
            continueButton.onClick.AddListener(HandleContinueClicked);

        if (hideOnAwake)
            gameObject.SetActive(false);
    }

    public void Show()
    {
        var parentCanvas = GetComponentInParent<Canvas>(true);
        if (parentCanvas != null && !parentCanvas.enabled)
            parentCanvas.enabled = true;

        transform.SetAsLastSibling();
        gameObject.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        var rect = GetComponent<RectTransform>();
        if (rect != null && rect.localScale == Vector3.zero)
            rect.localScale = Vector3.one;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void HandleContinueClicked()
    {
        Time.timeScale = 1f;
        Hide();

        var gm = FindObjectOfType<GameManager>();
        if (gm != null)
            gm.NextLevel();
    }
}


