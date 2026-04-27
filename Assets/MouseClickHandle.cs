using UnityEngine;
using UnityEngine.EventSystems; // 클릭 감지를 위해 필요합니다

public class MouseClickHandle : MonoBehaviour, IPointerDownHandler
{
    private AN_Button buttonScript;

    void Start()
    {
        // 이 오브젝트에 붙어있는 AN_Button 스크립트를 가져옵니다.
        buttonScript = GetComponent<AN_Button>();
    }

    // 마우스로 이 오브젝트를 클릭했을 때 호출됩니다.
    public void OnPointerDown(PointerEventData eventData)
    {
        if (buttonScript != null)
        {
            Debug.Log("레버 클릭됨! 문을 작동시킵니다.");

            // 기존 AN_Button의 로직을 강제로 실행시킵니다.
            // distance 체크 없이 바로 작동하게 하려면 아래처럼 변수를 직접 건드려줍니다.
            buttonScript.isOpened = !buttonScript.isOpened;
        }
    }
}