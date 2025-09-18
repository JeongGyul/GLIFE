using System.Collections;
using UnityEngine;
using TMPro;

// [System.Serializable]을 추가해야 Inspector 창에 노출됩니다.
[System.Serializable]
public class GuideStyle
{
    [TextArea(4, 10)]
    public string text; // 텍스트 내용
    public Vector2 panelSize; // 패널의 크기 (Width, Height)
    public float fontSize; // 텍스트의 폰트 크기
    public float displayTime = 5.0f; // ★ 이 문장만 보여질 시간 (초)
}

public class GuideManager : MonoBehaviour
{
    // Unity 에디터에서 연결할 UI 요소들
    public TextMeshProUGUI textMesh;
    public CanvasGroup canvasGroup;
    public RectTransform panelRectTransform; // 패널의 RectTransform을 연결

    // 설정값
    [Header("시간 설정")]
    public float fadeTime = 1.0f;
    public float delayBetweenTexts = 1.5f;

    // 표시할 안내 문구와 스타일 목록
    [Header("안내 문구 및 스타일")]
    public GuideStyle[] guideStyles;


    void Start()
    {
        StartCoroutine(StartGuide());
    }

    IEnumerator StartGuide()
    {
        canvasGroup.alpha = 0;
        yield return new WaitForSeconds(2f);

        // 모든 스타일을 순서대로 적용
        foreach (GuideStyle style in guideStyles)
        {
            // 1. 현재 스타일에 맞게 UI 속성 변경
            textMesh.text = style.text;
            panelRectTransform.sizeDelta = style.panelSize;
            textMesh.fontSize = style.fontSize;

            // 2. 안내문 Fade In
            yield return StartCoroutine(FadeCanvasGroup(1f, fadeTime));
            
            // 3. ★ 각 스타일에 지정된 시간 동안 대기
            yield return new WaitForSeconds(style.displayTime);
            
            // 4. 안내문 Fade Out
            yield return StartCoroutine(FadeCanvasGroup(0f, fadeTime));
            
            // 5. 다음 문장 전 대기
            yield return new WaitForSeconds(delayBetweenTexts);
        }
        
        gameObject.SetActive(false);
    }

    IEnumerator FadeCanvasGroup(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float time = 0;

        while (time < duration)
        {
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
    }
}