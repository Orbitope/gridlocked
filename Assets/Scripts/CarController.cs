using UnityEngine;
using UnityEngine.EventSystems;

public class CarController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private int _carIndex;
    private int _length;
    private bool _isHorizontal;
    private int _fixedAxis;

    private Vector2 _startDragPos;
    private int _startPosIndex;
    
    // Bounds given by GameManager for the current drag
    private int _minPos;
    private int _maxPos;

    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void Initialize(int carIndex, int length, bool isHorizontal, int fixedAxis)
    {
        _carIndex = carIndex;
        _length = length;
        _isHorizontal = isHorizontal;
        _fixedAxis = fixedAxis;

        // Size the RectTransform visually
        float carWidth = isHorizontal ? length : 1;
        float carHeight = isHorizontal ? 1 : length;
        // Assuming unit size is 100 pixels in canvas
        _rectTransform.sizeDelta = new Vector2(carWidth * 100, carHeight * 100);

        // Color goal car red
        if (carIndex == BitboardSolver.GOAL_CAR_INDEX)
        {
            GetComponent<UnityEngine.UI.Image>().color = new Color(0.9f, 0.2f, 0.2f);
        }
        else
        {
            // Random distinct colors
            GetComponent<UnityEngine.UI.Image>().color = Random.ColorHSV(0f, 1f, 0.5f, 0.8f, 0.8f, 1f);
        }
    }

    public void UpdateVisualPosition(int currentPos)
    {
        // 1 unit = 100 pixels
        float x = _isHorizontal ? currentPos * 100 : _fixedAxis * 100;
        float y = _isHorizontal ? _fixedAxis * 100 : currentPos * 100;
        
        _rectTransform.anchoredPosition = new Vector2(x, y);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _startDragPos = eventData.position;
        _startPosIndex = GameManager.Instance.CurrentState.GetCarPos(_carIndex);
        
        GameManager.Instance.GetDragBounds(_carIndex, out _minPos, out _maxPos);
        
        // Visual Juice: Scale up slightly when picked up
        transform.localScale = Vector3.one * 1.05f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - _startDragPos;
        
        // 1 grid unit = 100 screen pixels, adjust delta
        float axisDelta = _isHorizontal ? delta.x : delta.y;
        float gridDelta = axisDelta / 100f;
        
        float desiredPos = _startPosIndex + gridDelta;
        
        // Hard clamp to bounds
        float visualPos = Mathf.Clamp(desiredPos, _minPos, _maxPos);
        
        // Render instantly during drag
        float x = _isHorizontal ? visualPos * 100 : _fixedAxis * 100;
        float y = _isHorizontal ? _fixedAxis * 100 : visualPos * 100;
        
        _rectTransform.anchoredPosition = new Vector2(x, y);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Snap to nearest integer
        float currentAxisPos = _isHorizontal ? _rectTransform.anchoredPosition.x : _rectTransform.anchoredPosition.y;
        int finalPos = Mathf.RoundToInt(currentAxisPos / 100f);
        
        // Visual Juice: Reset scale
        transform.localScale = Vector3.one;

        GameManager.Instance.CommitMove(_carIndex, finalPos);
    }

    public void FlashHint()
    {
        StartCoroutine(FlashRoutine());
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        var img = GetComponent<UnityEngine.UI.Image>();
        Color original = img.color;
        
        // Flash bright white
        img.color = Color.white;
        yield return new WaitForSeconds(0.15f);
        img.color = original;
        yield return new WaitForSeconds(0.15f);
        img.color = Color.white;
        yield return new WaitForSeconds(0.15f);
        img.color = original;
    }
}
