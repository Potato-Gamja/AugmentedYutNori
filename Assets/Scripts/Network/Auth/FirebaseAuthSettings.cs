using UnityEngine;

// Firebase 구글 로그인에 필요한 설정값.
// Firebase 콘솔 > 프로젝트 설정 > 일반 > 내 앱(웹 앱) > "구성"에 나오는 firebaseConfig 값을 그대로 넣는다.
// 웹 앱 설정값은 브라우저에 공개되는 값이라 비밀이 아니다. PC와 웹 빌드가 같은 값을 쓴다.
[CreateAssetMenu(fileName = "FirebaseAuthSettings", menuName = "YutNori/Firebase Auth Settings")]
public class FirebaseAuthSettings : ScriptableObject
{
    [Header("Firebase 웹 앱 (firebaseConfig)")]
    [SerializeField] private string apiKey;
    [SerializeField] private string authDomain;     // 예) augmentedyutnori.firebaseapp.com
    [SerializeField] private string projectId;
    [SerializeField] private string appId;

    [Header("PC 로그인 대기 시간(초). 브라우저에서 로그인을 끝내지 않으면 취소된다")]
    [SerializeField] private float desktopTimeoutSec = 180f;

    public string ApiKey => apiKey;
    public string AuthDomain => authDomain;
    public string ProjectId => projectId;
    public string AppId => appId;
    public float DesktopTimeoutSec => desktopTimeoutSec;
}
