using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Game 씬의 실제 로컬 선택과 연출 취소를 Play 모드에서 검사한다. 게임/설정 파일을 저장하지 않는다.</summary>
[InitializeOnLoad]
public static class UIAugmentPlayVerification
{
    private const string Key="UIv2.Stage6.PlayVerification.";
    private static GameFlowController flow;
    private static IGameSession session;
    private static AugmentSelectPanel independent;
    private static double deadline,clickedAt;
    private static string chosen;
    private static int phase,team;
    static UIAugmentPlayVerification(){EditorApplication.update+=Tick;}

    [MenuItem("Tools/Yutnori/UIv2/Verify Augment Local Play")]
    public static void Start()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play를 종료해 주세요.");
        var original=SceneManager.GetActiveScene();if(original.isDirty)throw new InvalidOperationException("현재 씬의 변경을 먼저 저장하거나 취소해 주세요.");
        SessionState.SetBool(Key+"feedback",false);SessionState.SetBool(Key+"veteran",false);SessionState.SetInt(Key+"forcedTeam",-1);
        SessionState.SetString(Key+"original",original.path);SessionState.SetString(Key+"report","");SessionState.SetInt(Key+"count",0);SessionState.SetInt(Key+"independentCount",0);SessionState.SetBool(Key+"restore",false);SessionState.SetBool(Key+"running",true);
        var game=EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Single);var installer=UnityEngine.Object.FindFirstObjectByType<GameInstaller>();
        var so=new SerializedObject(installer);Require(so.FindProperty("defaultMode").enumValueIndex==(int)GameSessionMode.Local,"GameInstaller 기본 설정 Local");
        // 이번 검사는 로비에서 전달된 온라인 시작 정보가 없는 순수 Game 씬 실행이다.
        Require(typeof(GameInstaller).GetField("pendingGameStart",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)==null,"미소비 온라인 시작 정보 없음");
        EditorApplication.isPlaying=true;
    }
    [MenuItem("Tools/Yutnori/Augments/Verify Veteran Local Play")]
    public static void StartVeteran()
    {
        Start();
        SessionState.SetBool(Key+"veteran",true);
    }
    [MenuItem("Tools/Yutnori/UIv2/Verify Game Effect Feedback")]
    public static void StartFeedback()
    {
        StartVeteran();SessionState.SetBool(Key+"feedback",true);
    }
    private static T Private<T>(object owner,string name)
    {return (T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);}
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Pass(string message){SessionState.SetString(Key+"report",SessionState.GetString(Key+"report","")+message+": PASS\n");}
    private static AugmentSelectPanel RealPanel()
    {return UnityEngine.Object.FindFirstObjectByType<UIManager>().Get<AugmentSelectPanel>();}
    private static void BeginIndependent()
    {
        independent=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(UIAugmentPrefabBuilder.PanelPath),RealPanel().transform.parent).GetComponent<AugmentSelectPanel>();
        ReopenIndependent();
    }
    private static void ReopenIndependent()
    {
        independent.Setup(UIAugmentPrefabBuilder.Choices(),id=>SessionState.SetInt(Key+"independentCount",SessionState.GetInt(Key+"independentCount",0)+1),2);
        independent.Open();independent.GetComponentsInChildren<AugmentCardView>()[0].GetComponent<Button>().onClick.Invoke();clickedAt=EditorApplication.timeSinceStartup;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Key+"running",false))return;
        if(!EditorApplication.isPlaying)
        {
            if(SessionState.GetBool(Key+"restore",false)&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(Key+"running",false);string original=SessionState.GetString(Key+"original","");if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original,OpenSceneMode.Single);
            }
            return;
        }
        try
        {
            // Codex에서 에디터가 비활성인 동안에도 검증에 필요한 실제 PlayerLoop를 진행시킨다.
            EditorApplication.QueuePlayerLoopUpdate();
            Application.runInBackground=true;
            double now=EditorApplication.timeSinceStartup;
            if(flow==null)
            {
                flow=UnityEngine.Object.FindFirstObjectByType<GameFlowController>();if(flow==null)return;
                session=Private<IGameSession>(flow,"gameSession");if(session==null)return;Require(session is LocalGameSession,"실제 LocalGameSession");
                phase=0;team=0;deadline=now+15;session.AugmentSelected+=OnConfirmed;Pass("GameInstaller Local · LocalGameSession");
            }
            if(now>deadline)throw new TimeoutException("검증 단계 "+phase+" 시간 초과 · 확정 수="+SessionState.GetInt(Key+"count",0));
            if(phase==0)
            {
                if(SessionState.GetBool(Key+"veteran",false))
                {
                    Require(Private<BoardState>(flow,"boardState").Pieces.Count==0,"증강 선택 중 말 생성 없음");
                    foreach(var view in Private<System.Collections.Generic.List<PieceView>>(flow,"pieceViews"))Require(!view.gameObject.activeSelf,"선택 중 씬 말 숨김");
                    if(SessionState.GetInt(Key+"forcedTeam",-1)!=team)
                    {
                        var choices=new System.Collections.Generic.List<string>{team==0?"Veteran":"Hunter","Monk","Rethrow"};
                        Private<GameRules>(flow,"gameRules").SetAugmentChoices(team,choices);
                        typeof(GameFlowController).GetMethod("OnAugmentChoices",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(flow,new object[]{team,choices});
                        SessionState.SetInt(Key+"forcedTeam",team);
                    }
                }
                var panel=RealPanel();if(panel==null||!panel.IsOpen)return;
                var cards=panel.GetComponentsInChildren<AugmentCardView>();if(cards.Length!=3)return;
                Require(Private<int>(flow,"augmentSelectionTeam")==team,"선택 차례 "+team);Require(UIAugmentPrefabBuilder.Field<TMPro.TMP_Text>(panel,"titleText").color==AugmentCardView.TeamColor(team,true),"로컬 현재 팀 색");
                chosen=cards[0].AugmentId;cards[0].GetComponent<Button>().onClick.Invoke();cards[1].GetComponent<Button>().onClick.Invoke();clickedAt=now;phase=2;
            }
            else if(phase==2&&now-clickedAt>=0.12)
            {
                if(SessionState.GetInt(Key+"count",0)==team){var panel=RealPanel();Require(panel.IsOpen,"연출 중 패널 열림");var cards=panel.GetComponentsInChildren<AugmentCardView>();foreach(var card in cards)Require(!card.GetComponent<Button>().interactable,"연출 중 다른 카드 차단");Require(Mathf.Abs(UIAugmentPrefabBuilder.Field<RectTransform>(cards[0],"visual").localPosition.y-8)<0.1f,"선택 카드 0.1초 +8 이동");}phase=3;
            }
            else if(phase==3&&now-clickedAt>=0.5&&SessionState.GetInt(Key+"count",0)>team)
            {
                Require(SessionState.GetInt(Key+"count",0)==team+1,"확정 이벤트 정확히 한 번");Require(SessionState.GetFloat(Key+"delay",0)>=0.29f,"0.3초보다 먼저 확정하지 않음");Require(SessionState.GetString(Key+"chosen","")==chosen,"누른 후보 확정");Pass("팀 "+team+" · 선택 +8 · 0.3초 지연 · 중복 클릭 차단 · 실제 확정");
                team++;deadline=now+15;if(team<4){phase=0;return;}
                Require(Private<GameFlowState>(flow,"currentState")==GameFlowState.Throw,"4팀 선택 뒤 Throw 진입");Pass("4팀 실제 선택 완료 → 윷 던지기 단계");
                if(SessionState.GetBool(Key+"veteran",false))
                {
                    var board=Private<BoardState>(flow,"boardState");Require(board.Pieces.Count==15,"전체 말 15개");
                    Require(board.GetPiece(3)==null&&board.GetPiece(2)!=null,"노장 슬롯 0~2만 생성");
                    typeof(GameFlowController).GetMethod("CreatePieces",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(flow,null);
                    Require(board.Pieces.Count==15,"중복 말 생성 차단");
                    var hud=UnityEngine.Object.FindFirstObjectByType<UIManager>().Get<GameHudPanel>();
                    var playerView=Private<PlayerStatusView>(hud,"playerView");
                    Require(Private<int[]>(playerView,"pieceSlotCounts")[0]==3,"노장 HUD 슬롯 3개");
                    Pass("노장 실제 선택 → 말 3개 · 나머지 4개 · 중복 생성 차단 · HUD 3칸");
                }
                if(SessionState.GetBool(Key+"feedback",false))
                {
                    Private<GameRules>(flow,"gameRules").ApplyThrow(new[]{true,false,false,false},YutResult.BackDo);
                    Require(flow.CurrentTeam==1,"빽도 처리 후 실제 다음 팀 진행");
                    clickedAt=now;phase=9;deadline=now+15;
                }
                else {BeginIndependent();phase=4;}
            }
            else if(phase==9&&now-clickedAt>=0.3)
            {
                var hud=UnityEngine.Object.FindFirstObjectByType<UIManager>().Get<GameHudPanel>();
                var reveal=Private<YutResultRevealView>(hud,"resultRevealView");
                var root=Private<GameObject>(reveal,"revealRoot");
                Require(root.activeSelf&&root.GetComponent<CanvasGroup>().alpha>0.95f,"빽도 안내 표시·페이드 인");
                Require(!root.GetComponent<CanvasGroup>().blocksRaycasts,"안내가 다음 차례 입력을 막지 않음");
                Require(Private<TMPro.TMP_Text>(reveal,"title").text.Contains("차례 종료"),"차례 종료 제목");
                Pass("빽도 → 실제 다음 턴 진행 · 안내 페이드 인 · 클릭 통과");phase=10;
            }
            else if(phase==10&&now-clickedAt>=1.7)
            {
                var hud=UnityEngine.Object.FindFirstObjectByType<UIManager>().Get<GameHudPanel>();
                var reveal=Private<YutResultRevealView>(hud,"resultRevealView");
                Require(!Private<GameObject>(reveal,"revealRoot").activeSelf,"안내 자동 닫힘");
                Pass("빽도 안내 자동 닫힘 · 게임 진행 유지");BeginIndependent();phase=4;
            }
            else if(phase==4&&now-clickedAt>=0.1){independent.Close();phase=5;}
            else if(phase==5&&now-clickedAt>=0.5)
            {
                Require(SessionState.GetInt(Key+"independentCount",0)==0,"조기 Close 콜백 취소");Pass("0.3초 연출 중 외부 Close → 콜백 0회");ReopenIndependent();phase=6;
            }
            else if(phase==6&&now-clickedAt>=0.1){independent.gameObject.SetActive(false);phase=7;}
            else if(phase==7&&now-clickedAt>=0.5)
            {
                Require(SessionState.GetInt(Key+"independentCount",0)==0,"외부 비활성 콜백 취소");Pass("0.3초 연출 중 외부 비활성 → 콜백 0회");ReopenIndependent();phase=8;
            }
            else if(phase==8&&now-clickedAt>=0.5&&SessionState.GetInt(Key+"independentCount",0)>0)
            {
                Require(SessionState.GetInt(Key+"independentCount",0)==1&&!independent.IsOpen,"재열기 정상 확정 한 번");Pass("취소 뒤 Setup·Open 재사용 → 콜백 1회 · 닫힘");Finish(null);
            }
        }
        catch(Exception exception){Finish(exception);}
    }
    private static void OnConfirmed(int selectedTeam,IAugment augment)
    {SessionState.SetInt(Key+"count",SessionState.GetInt(Key+"count",0)+1);SessionState.SetString(Key+"chosen",augment.Id);SessionState.SetInt(Key+"confirmedTeam",selectedTeam);SessionState.SetFloat(Key+"delay",(float)(EditorApplication.timeSinceStartup-clickedAt));}
    private static void Finish(Exception exception)
    {
        if(session!=null)session.AugmentSelected-=OnConfirmed;
        string report=SessionState.GetString(Key+"report","")+(exception==null?"RESULT: PASS\n":"RESULT: FAIL · "+exception+"\n");
        Directory.CreateDirectory(UIScreenPreparation.Reports);File.WriteAllText(UIScreenPreparation.Reports+(SessionState.GetBool(Key+"feedback",false)?"game_effect_feedback_play_validation.txt":SessionState.GetBool(Key+"veteran",false)?"veteran_local_play_validation.txt":"stage6_local_play_validation.txt"),report);
        SessionState.SetBool(Key+"restore",true);EditorApplication.isPlaying=false;
        if(exception!=null)Debug.LogError(report);else Debug.Log(report);
    }
}
