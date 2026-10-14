using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YutNori.UI.Common;

/// <summary>명세 4장 대기실만 생성·검증한다. 가짜 방 데이터는 Editor 안에서만 사용한다.</summary>
public static class UIRoomPrefabBuilder
{
    public const string PanelPath="Assets/Prefabs/UIv2/Panels/RoomPanel.prefab";
    public const string SlotPath="Assets/Prefabs/UIv2/Items/RoomSlotCard.prefab";
    private const string RootPath="Assets/Prefabs/UI/UIRoot.prefab";
    public static readonly string[] Cases={"2인 · 나만 준비","3인 · 전원 준비 · 방장","4인 · 재연결 중","3인 · 전원 준비 · 일반 참가자"};
    private static TMP_FontAsset Jua=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.Jua);
    private static TMP_FontAsset Medium=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.MediumDynamic);
    private static TMP_FontAsset Bold=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.Bold);
    private static Color Hex(string code){Color color;ColorUtility.TryParseHtmlString("#"+code,out color);return color;}
    private static RectTransform Rect(string name,Transform parent,Vector2 size)
    {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.sizeDelta=size;return rect;}
    private static void Stretch(RectTransform rect)
    {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private static void Top(RectTransform rect,float x,float y,float width,float height)
    {rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
    private static void Center(RectTransform rect,float x,float y,float width,float height)
    {Top(rect,x,y,width,height);rect.pivot=Vector2.one*0.5f;}
    private static Sprite Sprite(string name)
    {
        string path=AssetDatabase.FindAssets(name+" t:Texture2D",new[]{"Assets/Art"}).Select(AssetDatabase.GUIDToAssetPath)
            .Single(item=>Path.GetFileNameWithoutExtension(item)==name);
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
    }
    private static Image Image(string name,Transform parent,string sprite,string color="FFFFFF",bool sliced=true)
    {var rect=Rect(name,parent,Vector2.zero);Stretch(rect);var image=rect.gameObject.AddComponent<Image>();image.sprite=Sprite(sprite);image.color=Hex(color);image.raycastTarget=false;image.type=sliced?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;return image;}
    private static TMP_Text Text(string name,Transform parent,string value,TMP_FontAsset font,float size,string color)
    {var rect=Rect(name,parent,new Vector2(340,50));var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=size;text.text=value;text.color=Hex(color);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;return text;}
    private static GameObject Common(string name,Transform parent)
    {return (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UICommonPrefabBuilder.Folder+name+".prefab"),parent);}
    private static void Bind(UnityEngine.Object owner,string field,UnityEngine.Object value)
    {var so=new SerializedObject(owner);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Array(UnityEngine.Object owner,string field,IEnumerable<UnityEngine.Object> values)
    {var so=new SerializedObject(owner);var property=so.FindProperty(field);var list=values.ToArray();property.arraySize=list.Length;for(int i=0;i<list.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=list[i];so.ApplyModifiedPropertiesWithoutUndo();}
    private static HorizontalLayoutGroup Row(RectTransform root,float spacing)
    {var row=root.gameObject.AddComponent<HorizontalLayoutGroup>();row.spacing=spacing;row.childAlignment=TextAnchor.MiddleCenter;row.childControlWidth=row.childControlHeight=true;row.childForceExpandWidth=row.childForceExpandHeight=false;return row;}
    private static void Size(GameObject root,float width,float height)
    {var layout=root.AddComponent<LayoutElement>();layout.minWidth=layout.preferredWidth=width;layout.minHeight=layout.preferredHeight=height;layout.flexibleWidth=layout.flexibleHeight=0;}
    private static GameObject Slot()
    {
        var root=Common("PaperCard",null);PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.name="RoomSlotCard";root.GetComponent<RectTransform>().sizeDelta=new Vector2(380,500);
        var group=root.AddComponent<CanvasGroup>();
        var band=Image("TeamBand",root.transform,"player_header");Top(band.rectTransform,20,20,340,56);
        var well=Image("IconWell",root.transform,"slot_well");Top(well.rectTransform,106,104,168,168);
        var icon=Image("Icon",root.transform,"profile_px_locked",sliced:false);Center(icon.rectTransform,190,188,128,128);icon.preserveAspect=true;icon.gameObject.AddComponent<PixelIconSampling>();
        var empty=Rect("EmptyIcon",root.transform,new Vector2(128,128));Center(empty,190,200,128,128);
        Image("Ring",empty,"btn_round_border","CBB08A",false);
        var question=Text("Question",empty,"?",Jua,64,"CBB08A");Stretch(question.rectTransform);
        var name=Text("Name",root.transform,"참가 대기",Jua,34,"A88E6E");Center(name.rectTransform,190,318,340,58);name.enableAutoSizing=true;name.fontSizeMax=36;name.fontSizeMin=22;
        var pill=Image("StatusPill",root.transform,"pill_fill","F3E6CF");Center(pill.rectTransform,190,435,176,46);
        var check=Image("Check",pill.transform,"icon_check",sliced:false);Center(check.rectTransform,25,23,26,26);check.preserveAspect=true;
        var status=Text("Status",pill.transform,"빈 자리",Medium,20,"A88E6E");Stretch(status.rectTransform);
        var view=root.AddComponent<RoomSlotPresentation>();view.Configure(group,root.transform.Find("Shadow").gameObject,pill,check.gameObject,name,status,Bold,Medium);view.Show(false,false,false);
        Size(root,380,500);return root;
    }
    // Button 자신을 줄의 직접 자식으로 둔다. 기존 SetActive(false)가 레이아웃에서도 자리를 없앤다.
    private static Button Action(string common,string name,Transform parent,float width,float height,string label,float fontSize,bool glow)
    {
        var outer=Common(common,parent);PrefabUtility.UnpackPrefabInstance(outer,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        var button=outer.GetComponentInChildren<Button>();var highlight=outer.transform.Find("Glow");if(highlight!=null){highlight.SetParent(button.transform,false);highlight.SetAsFirstSibling();}
        button.transform.SetParent(parent,false);button.name=name;UnityEngine.Object.DestroyImmediate(outer);
        var oldFace=button.GetComponent<Image>();var face=Image("Face",button.transform,oldFace.sprite.name);face.raycastTarget=true;face.color=oldFace.color;
        UnityEngine.Object.DestroyImmediate(oldFace);button.targetGraphic=face;face.transform.SetAsFirstSibling();if(highlight!=null)highlight.SetAsFirstSibling();
        var rect=button.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=Vector2.one*0.5f;rect.pivot=Vector2.one*0.5f;rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(width,height);
        var text=button.transform.Find("Label").GetComponent<TMP_Text>();text.text=label;text.fontSize=fontSize;text.rectTransform.sizeDelta=new Vector2(width-24,height-16);
        var border=button.transform.Find("Border");var subtitle=button.transform.Find("Subtitle");
        var presentation=button.GetComponent<ButtonPresentation>();presentation.Configure(button,face,border!=null?border.GetComponent<Image>():null,text,subtitle!=null?subtitle.GetComponent<TMP_Text>():null,highlight!=null?highlight.gameObject:null,common=="PrimaryButton");presentation.SetGlowVisible(glow);return button;
    }
    private static GameObject Panel()
    {
        var root=Rect("RoomPanel",null,new Vector2(1920,1080));Stretch(root);var panel=root.gameObject.AddComponent<RoomPanel>();
        var background=Image("Background",root,"bg_hanji_tile");background.type=UnityEngine.UI.Image.Type.Tiled;
        var title=Text("Title",root,"대기실",Jua,56,"3A2618");Center(title.rectTransform,960,86,800,80);
        var settings=Common("RoundIconButton",root);settings.name="SettingsButton";Center(settings.GetComponent<RectTransform>(),1840,76,64,64);Bind(panel,"settingsButton",settings.GetComponentInChildren<Button>());
        var codeCard=Common("PaperCard",root);codeCard.name="CodeCard";Top(codeCard.GetComponent<RectTransform>(),560,150,800,110);
        var codeLabel=Text("CodeLabel",codeCard.transform,"방 코드",Bold,22,"7A5C3E");Center(codeLabel.rectTransform,100,55,80,40);
        var code=Text("Code",codeCard.transform,"K7Q2MX",Jua,56,"3A2618");Top(code.rectTransform,180,20,230,66);code.alignment=TextAlignmentOptions.MidlineLeft;Bind(panel,"roomCodeText",code);
        var visibility=Image("Visibility",codeCard.transform,"pill_fill","F3E6CF");Center(visibility.rectTransform,516,55,160,36);
        var fit=visibility.gameObject.AddComponent<HorizontalLayoutGroup>();fit.padding=new RectOffset(18,18,0,0);fit.childAlignment=TextAnchor.MiddleCenter;fit.childControlWidth=fit.childControlHeight=true;fit.childForceExpandWidth=false;
        var fitter=visibility.gameObject.AddComponent<ContentSizeFitter>();fitter.horizontalFit=ContentSizeFitter.FitMode.PreferredSize;
        var visibilityText=Text("Label",visibility.transform,"공개방",Bold,18,"7A5C3E");Bind(panel,"roomVisibilityText",visibilityText);
        var copy=Action("SecondaryButton","Copy",codeCard.transform,170,72,"코드 복사",26,false);Top(copy.GetComponent<RectTransform>(),600,18,170,72);Bind(panel,"copyRoomCodeButton",copy);
        var slots=Rect("Slots",root,new Vector2(1616,500));Top(slots,152,300,1616,500);Row(slots,32);
        var slotObjects=new List<GameObject>();
        for(int i=0;i<4;i++){var slot=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SlotPath),slots);slot.name="Slot"+(i+1);slotObjects.Add(slot);}
        Array(panel,"playerSlots",slotObjects);Array(panel,"playerNameTexts",slotObjects.Select(item=>item.transform.Find("Name").GetComponent<TMP_Text>()));
        Array(panel,"playerStatusTexts",slotObjects.Select(item=>item.transform.Find("StatusPill/Status").GetComponent<TMP_Text>()));
        Array(panel,"playerSlotBackgrounds",slotObjects.Select(item=>item.transform.Find("TeamBand").GetComponent<Image>()));
        Array(panel,"playerProfileIcons",slotObjects.Select(item=>item.transform.Find("Icon").GetComponent<Image>()));
        Array(panel,"emptyProfileIconIndicators",slotObjects.Select(item=>item.transform.Find("EmptyIcon").gameObject));
        Array(panel,"profileIcons",AssetDatabase.FindAssets("t:Sprite",new[]{"Assets/Resources/ProfileIcons"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path=>path).Select(path=>AssetDatabase.LoadAssetAtPath<Sprite>(path)));
        var status=Text("Status",root,"",Medium,22,"7A5C3E");Center(status.rectTransform,960,868,1200,40);Bind(panel,"statusText",status);
        var hidden=Text("HiddenPlayers",root,"",Medium,20,"7A5C3E");hidden.gameObject.SetActive(false);Bind(panel,"playersText",hidden);
        var buttons=Rect("Buttons",root,new Vector2(800,88));Top(buttons,560,920,800,88);Row(buttons,40);
        var ready=Action("SecondaryButton","Ready",buttons,240,88,"준비",38,false);Size(ready.gameObject,240,88);
        var style=ready.gameObject.AddComponent<RoomReadyButtonPresentation>();style.Configure(new[]{Sprite("btn_throw_normal"),Sprite("btn_throw_hover"),Sprite("btn_throw_pressed"),Sprite("btn_throw_disabled")},new[]{Sprite("btn_rethrow_normal"),Sprite("btn_rethrow_hover"),Sprite("btn_rethrow_pressed"),Sprite("btn_rethrow_disabled")},ready.transform.Find("Border").GetComponent<Image>());style.SetReady(false);
        Bind(panel,"readyButton",ready);Bind(panel,"readyButtonText",ready.transform.Find("Label").GetComponent<TMP_Text>());
        var start=Action("PrimaryButton","Start",buttons,240,88,"게임 시작",38,true);Size(start.gameObject,240,88);Bind(panel,"startGameButton",start);start.gameObject.SetActive(false);
        var leave=Action("SecondaryButton","Leave",buttons,240,88,"나가기",38,false);Size(leave.gameObject,240,88);Bind(panel,"leaveButton",leave);
        return root.gameObject;
    }
    [MenuItem("Tools/Yutnori/UIv2/Generate Room Prefabs")]
    public static void Generate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play를 종료해 주세요.");
        Directory.CreateDirectory("Assets/Prefabs/UIv2/Items");AssetDatabase.Refresh();var scene=EditorSceneManager.NewPreviewScene();
        try{var slot=Slot();SceneManager.MoveGameObjectToScene(slot,scene);PrefabUtility.SaveAsPrefabAsset(slot,SlotPath);UnityEngine.Object.DestroyImmediate(slot);var panel=Panel();SceneManager.MoveGameObjectToScene(panel,scene);PrefabUtility.SaveAsPrefabAsset(panel,PanelPath);UnityEngine.Object.DestroyImmediate(panel);}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    [MenuItem("Tools/Yutnori/UIv2/Replace Room Reference")]
    public static void ReplaceReference()
    {
        var root=PrefabUtility.LoadPrefabContents(RootPath);var rows=new List<string>();
        try{var manager=root.GetComponentInChildren<UIManager>(true);var so=new SerializedObject(manager);var list=so.FindProperty("prefabs");int count=0;
            for(int i=0;i<list.arraySize;i++){var entry=list.GetArrayElementAtIndex(i);if(!(entry.objectReferenceValue is RoomPanel))continue;rows.Add("| "+RootPath+" | "+manager.name+" · UIManager.prefabs["+i+"] | "+AssetDatabase.GetAssetPath(entry.objectReferenceValue)+" | "+PanelPath+" |");entry.objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath).GetComponent<RoomPanel>();count++;}
            if(count!=1)throw new InvalidOperationException("RoomPanel 등록 수: "+count);so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,RootPath);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
        Directory.CreateDirectory(UIScreenPreparation.Reports);File.WriteAllText(UIScreenPreparation.Reports+"stage5_reference_changes.md","# 5단계 참조 교체\n\n| 파일 | 오브젝트·필드 | 이전 | 이후 |\n|---|---|---|---|\n"+string.Join("\n",rows)+"\n\n같은 타입은 한 번만 등록합니다. 기존 RoomPanel 프리팹은 보존했습니다.\n");
    }
    public static void ApplyCase(RoomPanel panel,int index)
    {
        int count=index==0?2:index==2?4:3;var room=new RoomStateMessage{roomCode="K7Q2MX",status="WAITING",currentPlayers=count,maxPlayers=4};
        string[] names={"윷잘알","도토리","빽도장인","모아니면도"};string[] icons={"icon_012","icon_008","icon_016","icon_022"};
        for(int i=0;i<count;i++)room.members.Add(new RoomPlayer{playerId="preview-"+i,nickname=names[i],iconId=icons[i],team=i,ready=index!=0||i==0});
        panel.SetInteraction(true);panel.ShowRoom(room,index==3?"preview-1":"preview-0");panel.ShowRoomVisibility(room.roomCode,true);
        panel.ShowReconnectingPlayers(index==2?new[]{"preview-2"}:new string[0]);panel.ConfigureGameStart(index!=3,()=>{});panel.ShowStatus("");
    }
    public sealed class Preview : IDisposable
    {
        public RoomPanel Panel;public RenderTexture Target;
        private Scene scene;private Camera camera;
        public Preview()
        {
            scene=EditorSceneManager.NewPreviewScene();var cameraObject=new GameObject("FHD Camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.orthographic=true;camera.orthographicSize=540;camera.transform.position=new Vector3(0,0,-10);
            var light=new GameObject("Directional Light");SceneManager.MoveGameObjectToScene(light,scene);light.AddComponent<Light>().type=LightType.Directional;
            var root=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(root,scene);var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;root.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
            Panel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath),root.transform).GetComponent<RoomPanel>();Panel.Init();
            Target=new RenderTexture(1920,1080,24);Target.Create();camera.targetTexture=Target;
        }
        public void Render()
        {
            foreach(var view in Panel.GetComponentsInChildren<ButtonPresentation>(true))view.Refresh();foreach(var pixel in Panel.GetComponentsInChildren<PixelIconSampling>(true))pixel.Refresh();
            Canvas.ForceUpdateCanvases();foreach(var text in Panel.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate(true);Canvas.ForceUpdateCanvases();camera.Render();
        }
        public void Save(string file)
        {Render();var previous=RenderTexture.active;Texture2D image=null;try{RenderTexture.active=Target;image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();Directory.CreateDirectory(UIScreenPreparation.Reports);File.WriteAllBytes(UIScreenPreparation.Reports+file,image.EncodeToPNG());}finally{RenderTexture.active=previous;if(image!=null)UnityEngine.Object.DestroyImmediate(image);}}
        public void Dispose(){camera.targetTexture=null;Target.Release();UnityEngine.Object.DestroyImmediate(Target);EditorSceneManager.ClosePreviewScene(scene);}
    }
    [MenuItem("Tools/Yutnori/UIv2/Capture Room States FHD")]
    public static void Capture()
    {using(var preview=new Preview()){for(int i=0;i<4;i++){ApplyCase(preview.Panel,i);preview.Save(new[]{"stage5_room_2p_fhd.png","stage5_room_3p_host_fhd.png","stage5_room_4p_reconnect_fhd.png","stage5_room_nonhost_fhd.png"}[i]);}}}
    private static T Field<T>(UnityEngine.Object owner,string name) where T:UnityEngine.Object
    {return (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;}
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException("[Room UI] "+message);}
    [MenuItem("Tools/Yutnori/UIv2/Validate Room UI")]
    public static string Validate()
    {
        var results=new List<string>();using(var preview=new Preview())
        {
            var panel=preview.Panel;var start=Field<Button>(panel,"startGameButton");var ready=Field<Button>(panel,"readyButton");var leave=Field<Button>(panel,"leaveButton");
            foreach(string field in new[]{"playerSlots","playerNameTexts","playerStatusTexts","playerSlotBackgrounds","playerProfileIcons","emptyProfileIconIndicators"}){var array=new SerializedObject(panel).FindProperty(field);Require(array.arraySize==4,field);for(int i=0;i<4;i++)Require(array.GetArrayElementAtIndex(i).objectReferenceValue!=null,field+"["+i+"]");}
            results.Add("자리 카드 4개 · 기존 배열 6종 순서와 연결: PASS");
            ApplyCase(panel,0);preview.Render();Require(!start.gameObject.activeSelf,"2인 미준비 시작 숨김");Require(((Image)ready.targetGraphic).sprite.name=="btn_rethrow_normal","준비 완료 보조 버튼");
            var empty=panel.transform.Find("Slots/Slot3");Require(empty.GetComponent<CanvasGroup>().alpha==0.7f&&!empty.Find("Shadow").gameObject.activeSelf&&!empty.Find("StatusPill").GetComponent<Image>().enabled,"빈 자리 스타일");
            float readyX=ready.transform.localPosition.x,leaveX=leave.transform.localPosition.x;Require(Mathf.Abs((readyX+leaveX)-800)<1,"두 버튼 중앙 정렬");
            Require(Field<TMP_Text>(panel,"roomCodeText").text=="K7Q2MX","코드만 표시");results.Add("2인 상태 · 준비 취소 모양 · 빈 자리 · 코드 · 두 버튼 중앙 정렬: PASS");
            ApplyCase(panel,1);preview.Render();Require(start.gameObject.activeSelf,"3인 전원 준비 방장 시작 표시");Require(Mathf.Abs(start.transform.localPosition.x-400)<1,"시작 가운데 정렬");
            ApplyCase(panel,3);preview.Render();Require(!start.gameObject.activeSelf,"일반 참가자 시작 숨김");
            ApplyCase(panel,2);preview.Render();Require(!start.gameObject.activeSelf,"재연결 중 시작 숨김");var slot=panel.transform.Find("Slots/Slot3");Require(slot.Find("StatusPill/Status").GetComponent<TMP_Text>().text=="재연결 대기 중..."&&!slot.Find("StatusPill/Check").gameObject.activeSelf,"해당 플레이어 재연결 알약");
            panel.ShowReconnectingPlayers(new string[0]);Require(start.gameObject.activeSelf,"재연결 복귀 시작 표시");panel.ConfigureGameStart(true,null);Require(!start.gameObject.activeSelf,"실제 시작 연결 없음 숨김");results.Add("3인 방장/일반 · 4인 재연결/복귀 · 요청 연결 없는 시작 숨김: PASS");
            ApplyCase(panel,0);int readyRequests=0,leaves=0,settings=0,starts=0;bool requested=true;panel.ReadyRequested+=value=>{readyRequests++;requested=value;};panel.LeaveRequested+=()=>leaves++;panel.SettingsRequested+=()=>settings++;
            ready.onClick.Invoke();Require(readyRequests==1&&!requested&&Field<TMP_Text>(panel,"readyButtonText").text=="준비 취소","준비 취소 이벤트 · 서버 응답 대기");
            var room=new RoomStateMessage{roomCode="K7Q2MX",status="WAITING",members=new List<RoomPlayer>{new RoomPlayer{playerId="preview-1",nickname="도토리",team=1,iconId="icon_008",ready=true},new RoomPlayer{playerId="preview-0",nickname="윷잘알",team=0,iconId="icon_012",ready=false}}};panel.ShowRoom(room,"preview-0");ready.onClick.Invoke();Require(readyRequests==2&&requested&&((Image)ready.targetGraphic).sprite.name=="btn_throw_normal","준비 전 주 버튼 및 이벤트");
            Require(panel.transform.Find("Slots/Slot1/Name").GetComponent<TMP_Text>().text=="윷잘알  (나)","서버 팀 순서 유지");
            leave.onClick.Invoke();Field<Button>(panel,"settingsButton").onClick.Invoke();Require(leaves==1&&settings==1,"나가기·설정 이벤트");
            string clipboard=GUIUtility.systemCopyBuffer;try{Field<Button>(panel,"copyRoomCodeButton").onClick.Invoke();Require(GUIUtility.systemCopyBuffer=="K7Q2MX","방 코드 복사 이벤트");}finally{GUIUtility.systemCopyBuffer=clipboard;}
            ApplyCase(panel,1);panel.ConfigureGameStart(true,()=>starts++);start.onClick.Invoke();Require(starts==1&&!start.interactable&&!ready.interactable&&!leave.interactable,"시작 이벤트 및 중복 입력 비활성");
            results.Add("준비·취소 · 서버 응답 후 갱신 · 팀 정렬 · 나가기·설정·복사·시작 이벤트: PASS");
        }
        string report=string.Join("\n",results)+"\n";Directory.CreateDirectory(UIScreenPreparation.Reports);File.WriteAllText(UIScreenPreparation.Reports+"stage5_validation.txt",report);return report;
    }
}

public sealed class UIRoomPreviewWindow : EditorWindow
{
    private UIRoomPrefabBuilder.Preview preview;private int selected;
    [MenuItem("Tools/Yutnori/UIv2/Room State Preview")]
    public static void ShowWindow(){GetWindow<UIRoomPreviewWindow>("Room UI Preview").Show();}
    private void OnEnable(){if(AssetDatabase.LoadAssetAtPath<GameObject>(UIRoomPrefabBuilder.PanelPath)!=null){preview=new UIRoomPrefabBuilder.Preview();UIRoomPrefabBuilder.ApplyCase(preview.Panel,selected);}}
    private void OnDisable(){if(preview!=null){preview.Dispose();preview=null;}}
    private void OnGUI()
    {
        if(preview==null){EditorGUILayout.HelpBox("Generate Room Prefabs를 먼저 실행해 주세요.",UnityEditor.MessageType.Info);return;}
        int next=EditorGUILayout.Popup("가짜 방 상태",selected,UIRoomPrefabBuilder.Cases);if(next!=selected){selected=next;UIRoomPrefabBuilder.ApplyCase(preview.Panel,selected);}
        if(GUILayout.Button("4가지 상태 FHD 캡처 저장"))UIRoomPrefabBuilder.Capture();
        preview.Render();var rect=GUILayoutUtility.GetRect(1920,1080,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));GUI.DrawTexture(rect,preview.Target,ScaleMode.ScaleToFit,false);
    }
}
