using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YutNori.UI.Common;

/// <summary>서버·PlayerPrefs 쓰기 없이 기존 표시 메서드에 샘플을 넣는 Editor 도구.</summary>
public sealed class UIPopupPreview : IDisposable
{
    public UIBase Panel;public RenderTexture Target;
    private Scene scene;private Camera camera;
    public static void Call(object owner,string method,params object[] args)
    {owner.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,args);}
    public static T Private<T>(object owner,string name)
    {return (T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);}
    public UIPopupPreview(string name,bool duplicate=false)
    {
        scene=EditorSceneManager.NewPreviewScene();var cameraRoot=new GameObject("FHD Camera");SceneManager.MoveGameObjectToScene(cameraRoot,scene);camera=cameraRoot.AddComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=540;
        var light=new GameObject("Directional Light");SceneManager.MoveGameObjectToScene(light,scene);light.AddComponent<Light>().type=LightType.Directional;
        var root=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(root,scene);var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;root.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
        var background=new GameObject("Background",typeof(RectTransform),typeof(Image)).GetComponent<Image>();background.transform.SetParent(root.transform,false);background.rectTransform.anchorMin=Vector2.zero;background.rectTransform.anchorMax=Vector2.one;background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;background.sprite=UIPopupPrefabBuilder.Sprite("bg_hanji_tile");background.type=Image.Type.Tiled;
        if(name=="GachaResultView")
        {
            var under=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(UIPopupPrefabBuilder.Folder+"GachaPanel.prefab"),root.transform).GetComponent<GachaPanel>();under.Init();Call(under,"ShowGold",1250);UIPopupPrefabBuilder.Field<Button>(under,"drawButton").interactable=true;
        }
        Panel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(UIPopupPrefabBuilder.Folder+name+".prefab"),root.transform).GetComponent<UIBase>();Panel.Init();
        // OnOpen은 네트워크를 조회하므로 호출하지 않는다. 표시용 메서드만 아래에서 사용한다.
        if(Panel is GachaPanel gacha){Call(gacha,"ShowGold",1250);UIPopupPrefabBuilder.Field<Button>(gacha,"drawButton").interactable=true;}
        if(Panel is GachaResultView result){var catalog=new ProfileIconCatalog(JsonUtility.FromJson<ProfileIconDefinitionFile>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/profile_icons.json").text));result.Show(new GachaResultResponse{iconId="icon_016",duplicate=duplicate,count=2,gold=1050,price=200},catalog.Find("icon_016").name,Resources.Load<Sprite>("ProfileIcons/icon_016"));Call(result,"OnClose");}
        if(Panel is ProfilePanel profile)
        {
            var owned=Private<HashSet<string>>(profile,"ownedIconIds");for(int i=1;i<=22;i++)if(i!=1&&i!=3)owned.Add("icon_"+i.ToString("D3"));
            Call(profile,"ApplyProfile",new PlayerProfileResponse{nickname="윷잘알",iconId="icon_016",gold=1250});profile.ShowProfile("abcd1234567890123456wxyz","윷잘알",Resources.Load<Sprite>("ProfileIcons/icon_016"));Call(profile,"SetBusy",false);
        }
        if(Panel is AchievementPanel achievement)
        {
            Call(achievement,"Build",new List<AchievementEntry>{new AchievementEntry{id="login5",name="꾸준한 출석",description="5일 접속하기",progress=3,goal=5,rewardGold=300},new AchievementEntry{id="game1",name="첫 번째 경기",description="게임 1판 완료하기",progress=1,goal=1,rewardGold=200,achieved=true,claimed=true},new AchievementEntry{id="login1",name="첫 출석",description="하루 접속하기",progress=1,goal=1,rewardGold=100,achieved=true}});Call(achievement,"ShowGold",1250);
        }
        if(Panel is SettingsPanel settings)settings.SetExitGameVisible(true);
        if(Panel is ReconnectPanel reconnect)reconnect.ShowRoom("K7Q2MX");
        Target=new RenderTexture(1920,1080,24);Target.Create();camera.targetTexture=Target;
    }
    public void Render(){foreach(var button in Panel.transform.parent.GetComponentsInChildren<ButtonPresentation>(true))button.Refresh();foreach(var pixel in Panel.transform.parent.GetComponentsInChildren<PixelIconSampling>(true))pixel.Refresh();foreach(var status in Panel.GetComponentsInChildren<InstructionStatusPresentation>(true))status.Refresh();Canvas.ForceUpdateCanvases();foreach(var text in Panel.transform.parent.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate(true);Canvas.ForceUpdateCanvases();camera.Render();}
    public void Save(string file){Render();var previous=RenderTexture.active;Texture2D image=null;try{RenderTexture.active=Target;image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();Directory.CreateDirectory(UIScreenPreparation.Reports);File.WriteAllBytes(UIScreenPreparation.Reports+file,image.EncodeToPNG());}finally{RenderTexture.active=previous;if(image!=null)UnityEngine.Object.DestroyImmediate(image);}}
    public void Dispose(){camera.targetTexture=null;Target.Release();UnityEngine.Object.DestroyImmediate(Target);EditorSceneManager.ClosePreviewScene(scene);}
    [MenuItem("Tools/Yutnori/UIv2/Capture Popup States FHD")]
    public static void Capture()
    {
        foreach(string name in UIPopupPrefabBuilder.Names)using(var preview=new UIPopupPreview(name))preview.Save("stage7_"+name+"_fhd.png");
        using(var preview=new UIPopupPreview("GachaResultView",true))preview.Save("stage7_gacha_duplicate_fhd.png");
        using(var preview=new UIPopupPreview("GachaResultView"))preview.Save("stage7_gacha_new_fhd.png");
        using(var preview=new UIPopupPreview("AchievementPanel"))preview.Save("stage7_achievement_states_fhd.png");
        using(var preview=new UIPopupPreview("ProfilePanel")){UIPopupPrefabBuilder.Field<ScrollRect>(preview.Panel,"iconScrollRect").verticalNormalizedPosition=0;preview.Save("stage7_profile_scroll_bottom_fhd.png");}
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception("[Popup UI] "+message);}
    [MenuItem("Tools/Yutnori/UIv2/Validate Popup UI")]
    public static string Validate()
    {
        var report=new List<string>();
        foreach(string name in UIPopupPrefabBuilder.Names)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(UIPopupPrefabBuilder.Folder+name+".prefab").GetComponent<UIBase>();Require(prefab.Layer==UILayer.Popup,name+" popup layer");
            foreach(var field in prefab.GetType().GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Where(field=>Attribute.IsDefined(field,typeof(SerializeField))&&typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)))Require((UnityEngine.Object)field.GetValue(prefab)!=null,name+" field "+field.Name);
            using(var preview=new UIPopupPreview(name)){preview.Render();if(name!="GachaResultView"&&name!="ReconnectPanel"){var close=UIPopupPrefabBuilder.Field<Button>(preview.Panel,"closeButton");Require(close.transform.parent.name=="CloseButton","X connection "+name);}}
        }
        report.Add("팝업 7종 · 기존 Object SerializeField 연결 · X/하단 닫기 연결: PASS");
        using(var preview=new UIPopupPreview("ProfilePanel"))
        {
            var profile=(ProfilePanel)preview.Panel;preview.Render();var content=UIPopupPrefabBuilder.Field<Transform>(profile,"iconOptionContainer");var scroll=UIPopupPrefabBuilder.Field<ScrollRect>(profile,"iconScrollRect");Require(content.childCount==22,"프로필 22칸");float top=scroll.content.anchoredPosition.y;scroll.verticalNormalizedPosition=0;preview.Render();Require(scroll.content.anchoredPosition.y>top+100&&scroll.content.rect.height>scroll.viewport.rect.height,"실제 세로 스크롤 이동");
            foreach(var icon in content.GetComponentsInChildren<PixelIconSampling>()){var sprite=icon.GetComponent<Image>().sprite;Require(sprite!=null&&sprite.texture.filterMode==FilterMode.Point,"픽셀 Point");}
            profile.ShowProfile("123456789012","닉네임");Require(UIPopupPrefabBuilder.Field<TMP_Text>(profile,"playerIdText").text=="123456789012","12자 보존");profile.ShowProfile("1234567890123","닉네임");Require(UIPopupPrefabBuilder.Field<TMP_Text>(profile,"playerIdText").text=="1234…0123","13자 생략");Require(UIPopupPrefabBuilder.Field<TMP_Text>(profile,"goldText").text==1250.ToString("N0"),"골드 숫자만");
            int logout=0;profile.LogoutRequested+=()=>logout++;UIPopupPrefabBuilder.Field<Button>(profile,"logoutButton").onClick.Invoke();Require(logout==1,"기존 로그아웃 이벤트");
        }
        report.Add("프로필 22칸 · 스크롤 이동 · Point · ID 12/13자 경계 · 골드 · 로그아웃 이벤트: PASS");
        using(var preview=new UIPopupPreview("AchievementPanel"))
        {
            var items=preview.Panel.GetComponentsInChildren<AchievementItemView>();Require(items.Length==3&&items[0].Entry.achieved&&!items[0].Entry.claimed&&!items[1].Entry.achieved&&items[2].Entry.claimed,"기존 3상태 정렬");
            Require(UIPopupPrefabBuilder.Field<Image>(items[0],"claimableBorder").gameObject.activeSelf&&UIPopupPrefabBuilder.Field<GameObject>(items[2],"claimedCheck").activeSelf,"테두리/체크");
            int claims=0;foreach(var item in items){Call(item,"Awake");item.ClaimClicked+=view=>claims++;UIPopupPrefabBuilder.Field<Button>(item,"claimButton").onClick.Invoke();}Require(claims==1,"받을 수 있는 줄만 이벤트");items[0].SetBusy(true);UIPopupPrefabBuilder.Field<Button>(items[0],"claimButton").onClick.Invoke();Require(claims==1,"받는 중 중복 차단");items[0].MarkClaimed();Require(UIPopupPrefabBuilder.Field<GameObject>(items[0],"claimedCheck").activeSelf&&!UIPopupPrefabBuilder.Field<Image>(items[0],"claimableBorder").gameObject.activeSelf,"MarkClaimed 표시");
        }
        report.Add("업적 기존 정렬 · 받기/진행/받음 · Accent/체크 · 받기/Busy 이벤트·수령 후 표시: PASS");
        foreach(bool duplicate in new[]{false,true})using(var preview=new UIPopupPreview("GachaResultView",duplicate)){var view=(GachaResultView)preview.Panel;Require(UIPopupPrefabBuilder.Field<Image>(view,"resultPill").color==UIPopupPrefabBuilder.Hex(duplicate?"F3E6CF":"E0950F"),"새/중복 알약");UIPopupPrefabBuilder.Field<Button>(view,"confirmButton").onClick.Invoke();Require(!view.IsOpen,"결과 확인 닫기");}
        using(var preview=new UIPopupPreview("CollectionPanel")){preview.Render();var cards=preview.Panel.GetComponentsInChildren<AugmentCardView>();Require(cards.Length==11,"도감 11종");foreach(var card in cards){Require(!card.GetComponent<Button>().interactable,"도감 클릭 없음");var desc=UIPopupPrefabBuilder.Field<TMP_Text>(card,"augmentDescription");Require(desc.textInfo.lineCount<=3&&!desc.isTextTruncated,"작은 카드 설명 "+card.AugmentId);}}
        using(var preview=new UIPopupPreview("SettingsPanel")){var settings=(SettingsPanel)preview.Panel;settings.SetExitGameVisible(false);Require(!UIPopupPrefabBuilder.Field<Button>(settings,"exitGameButton").gameObject.activeSelf,"설정 나가기 숨김");settings.SetExitGameVisible(true);int exits=0;settings.ExitGameRequested+=()=>exits++;UIPopupPrefabBuilder.Field<Button>(settings,"exitGameButton").onClick.Invoke();Require(exits==1,"설정 나가기 이벤트");}
        report.Add("뽑기 새/중복 · 확인 닫기 · 도감 11종 설명/클릭 없음 · 설정 나가기 표시·이벤트: PASS");
        string result=string.Join("\n",report)+"\n";Directory.CreateDirectory(UIScreenPreparation.Reports);File.WriteAllText(UIScreenPreparation.Reports+"stage7_validation.txt",result);return result;
    }
}

public sealed class UIPopupPreviewWindow : EditorWindow
{
    private UIPopupPreview preview;private int selected;private bool duplicate;
    [MenuItem("Tools/Yutnori/UIv2/Popup State Preview")]
    public static void ShowWindow(){GetWindow<UIPopupPreviewWindow>("Popup UI Preview").Show();}
    private void OnEnable(){if(AssetDatabase.LoadAssetAtPath<GameObject>(UIPopupPrefabBuilder.Folder+"GachaPanel.prefab")!=null)preview=new UIPopupPreview(UIPopupPrefabBuilder.Names[selected],duplicate);}
    private void OnDisable(){if(preview!=null){preview.Dispose();preview=null;}}
    private void OnGUI(){if(preview==null)return;EditorGUI.BeginChangeCheck();selected=EditorGUILayout.Popup("팝업",selected,UIPopupPrefabBuilder.Names);duplicate=EditorGUILayout.Toggle("중복 결과",duplicate);if(EditorGUI.EndChangeCheck()){preview.Dispose();preview=new UIPopupPreview(UIPopupPrefabBuilder.Names[selected],duplicate);}if(preview.Panel is ProfilePanel){var scroll=UIPopupPrefabBuilder.Field<ScrollRect>(preview.Panel,"iconScrollRect");scroll.verticalNormalizedPosition=EditorGUILayout.Slider("아이콘 스크롤",scroll.verticalNormalizedPosition,0,1);}preview.Render();var rect=GUILayoutUtility.GetRect(1920,1080,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));GUI.DrawTexture(rect,preview.Target,ScaleMode.ScaleToFit,false);}
}
