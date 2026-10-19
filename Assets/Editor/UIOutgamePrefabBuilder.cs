using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YutNori.UI.Common;

/// <summary>명세 3장 UIv2 메인 메뉴와 게임 입장 카드·창을 재생성한다.</summary>
public static class UIOutgamePrefabBuilder
{
    public const string Folder = "Assets/Prefabs/UIv2/Panels/";
    private const string RootPath = "Assets/Prefabs/UI/UIRoot.prefab";
    private static TMP_FontAsset Jua => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.Jua);
    private static TMP_FontAsset Medium => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.MediumDynamic);
    private static TMP_FontAsset Bold => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.Bold);
    private static Color Hex(string value) { Color color; ColorUtility.TryParseHtmlString("#"+value,out color); return color; }
    private static RectTransform Rect(string name,Transform parent,Vector2 size)
    { var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.sizeDelta=size;return rect; }
    private static void Stretch(RectTransform rect)
    {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private static void TopLeft(RectTransform rect,float x,float y,Vector2 size)
    {rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=size;}
    private static void Center(RectTransform rect,float x,float y,Vector2 size)
    {rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=Vector2.one*0.5f;rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=size;}
    private static Sprite Sprite(string name)
    {
        var path=AssetDatabase.FindAssets(name+" t:Texture2D",new[]{"Assets/Art"}).Select(AssetDatabase.GUIDToAssetPath)
            .Single(item=>Path.GetFileNameWithoutExtension(item)==name);
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
    }
    private static Image MakeImage(string name,Transform parent,Sprite sprite,Color color,bool raycast=false)
    {
        var rect=Rect(name,parent,Vector2.zero);Stretch(rect);var image=rect.gameObject.AddComponent<Image>();
        image.sprite=sprite;image.color=color;image.raycastTarget=raycast;return image;
    }
    private static TMP_Text Text(string name,Transform parent,string value,TMP_FontAsset font,float size,string color)
    {
        var rect=Rect(name,parent,new Vector2(540,40));var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font;text.fontSize=size;text.text=value;text.color=Hex(color);text.fontStyle=FontStyles.Normal;
        text.raycastTarget=false;text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;return text;
    }
    private static GameObject Common(string name,Transform parent)
    {return (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UICommonPrefabBuilder.Folder+name+".prefab"),parent);}
    private static void Bind(UnityEngine.Object target,string name,UnityEngine.Object value)
    {var serialized=new SerializedObject(target);var field=serialized.FindProperty(name);if(field==null)throw new InvalidOperationException("필드 없음 "+name);field.objectReferenceValue=value;serialized.ApplyModifiedPropertiesWithoutUndo();}
    private static Button Card(Transform grid,string name,string label,string sub,string iconName)
    {
        var slot=Rect(name,grid,new Vector2(340,390));var paper=Common("PaperCard",slot);paper.name="Card";
        Stretch(paper.GetComponent<RectTransform>());
        var face=paper.transform.Find("Paper").GetComponent<Image>();face.raycastTarget=true;
        var button=paper.AddComponent<Button>();button.targetGraphic=face;button.transition=Selectable.Transition.None;
        button.navigation=new Navigation{mode=Navigation.Mode.None};paper.AddComponent<MenuCardInteraction>();
        var well=MakeImage("Well",paper.transform,Sprite("slot_well"),Color.white);well.type=Image.Type.Sliced;
        TopLeft(well.rectTransform,40,36,new Vector2(260,200));
        if(iconName!=null)
        {var icon=MakeImage("Icon",paper.transform,Sprite(iconName),Color.white);Center(icon.rectTransform,170,136,new Vector2(170,170));icon.preserveAspect=true;}
        else
        {
            string[] ids={"icon_012","icon_008","icon_022"};float[] x={100,170,240};float[] size={96,112,96};
            for(int i=0;i<3;i++)
            {var icon=MakeImage(ids[i],paper.transform,Resources.Load<Sprite>(ProfileIconCatalog.SpritePath(ids[i])),Color.white);Center(icon.rectTransform,x[i],136,new Vector2(size[i],size[i]));icon.preserveAspect=true;icon.gameObject.AddComponent<PixelIconSampling>();}
            Spark(paper.transform,"Spark1",66,70,24);Spark(paper.transform,"Spark2",272,78,18);Spark(paper.transform,"Spark3",266,202,14);
        }
        var title=Text("Label",paper.transform,label,Jua,42,"3A2618");Center(title.rectTransform,170,284,new Vector2(300,60));
        var description=Text("Description",paper.transform,sub,Medium,20,"7A5C3E");Center(description.rectTransform,170,332,new Vector2(310,32));
        var badge=MakeImage("NewBadge",paper.transform,Sprite("badge_new"),Color.white);Center(badge.rectTransform,308,32,new Vector2(28,28));badge.gameObject.SetActive(false);
        return button;
    }
    private static void Spark(Transform parent,string name,float x,float y,float size)
    {var spark=MakeImage(name,parent,Sprite("fx_sparkle"),Hex("E0950F"));Center(spark.rectTransform,x,y,new Vector2(size,size));}
    private static GameObject MainMenu()
    {
        var root=Rect("MainMenuPanel",null,new Vector2(1920,1080));Stretch(root);var panel=root.gameObject.AddComponent<MainMenuPanel>();
        var background=MakeImage("Background",root,Sprite("bg_hanji_tile"),Color.white);background.type=Image.Type.Tiled;
        var profile=Common("PaperCard",root);profile.name="ProfileChip";TopLeft(profile.GetComponent<RectTransform>(),32,24,new Vector2(420,104));
        var portrait=MakeImage("Portrait",profile.transform,Sprite("profile_px_locked"),Color.white);Center(portrait.rectTransform,60,52,new Vector2(64,64));portrait.preserveAspect=true;portrait.gameObject.AddComponent<PixelIconSampling>();
        var nickname=Text("Name",profile.transform,"닉네임 미설정",Jua,34,"3A2618");TopLeft(nickname.rectTransform,114,12,new Vector2(290,44));nickname.alignment=TextAlignmentOptions.MidlineLeft;
        nickname.enableAutoSizing=true;nickname.fontSizeMax=34;nickname.fontSizeMin=18;
        var gold=Common("GoldDisplay",root);gold.name="Gold";var goldRect=gold.GetComponent<RectTransform>();goldRect.anchorMin=goldRect.anchorMax=new Vector2(1,1);goldRect.pivot=new Vector2(1,1);goldRect.anchoredPosition=new Vector2(-170,-36);
        var amount=gold.transform.Find("Amount").GetComponent<TMP_Text>();amount.text="0";
        var settings=Common("RoundIconButton",root);settings.name="SettingsButton";var settingsRect=settings.GetComponent<RectTransform>();settingsRect.anchorMin=settingsRect.anchorMax=new Vector2(1,1);settingsRect.anchoredPosition=new Vector2(-80,-76);
        var status=Text("Status",root,"",Medium,20,"7A5C3E");status.rectTransform.anchorMin=status.rectTransform.anchorMax=new Vector2(1,0);status.rectTransform.anchoredPosition=new Vector2(-460,40);status.rectTransform.sizeDelta=new Vector2(720,32);
        var grid=Rect("MenuGrid",root,new Vector2(720,820));grid.anchorMin=grid.anchorMax=new Vector2(1,0.5f);grid.pivot=new Vector2(1,0.5f);grid.anchoredPosition=new Vector2(-120,-30);
        var layout=grid.gameObject.AddComponent<GridLayoutGroup>();layout.cellSize=new Vector2(340,390);layout.spacing=new Vector2(40,40);layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=2;
        Bind(panel,"gachaButton",Card(grid,"GachaCard","뽑기","프로필 아이콘 뽑기",null));
        Bind(panel,"collectionButton",Card(grid,"AugmentCard","직업","직업 도감","menu_augment"));
        Bind(panel,"achievementButton",Card(grid,"AchievementCard","업적","업적을 달성하고 골드 받기","menu_achievement"));
        Bind(panel,"profileButton",Card(grid,"ProfileCard","내 정보","프로필 아이콘 · 닉네임","menu_profile_icon"));
        Bind(panel,"settingsButton",settings.GetComponentInChildren<Button>());Bind(panel,"statusText",status);
        Bind(panel,"profileIcon",portrait);Bind(panel,"nicknameText",nickname);Bind(panel,"goldText",amount);
        UITutorialBuilder.ConfigureMenu(root.gameObject);
        return root.gameObject;
    }
    private static Button ActionButton(string commonName,string name,Transform parent,float x,float y,float width,float height,string label,float fontSize,bool glow)
    {
        var root=Common(commonName,parent);root.name=name;TopLeft(root.GetComponent<RectTransform>(),x,y,new Vector2(width,height));
        var button=root.GetComponentInChildren<Button>();var text=root.transform.Find("Button/Label").GetComponent<TMP_Text>();text.text=label;text.fontSize=fontSize;
        text.rectTransform.sizeDelta=new Vector2(width-32,Mathf.Max(50,fontSize+12));
        root.GetComponentInChildren<ButtonPresentation>().SetGlowVisible(glow);return button;
    }
    private static GameObject Window(Transform parent,string name,Vector2 size,string title,string sub)
    {
        var root=Common("PopupFrame",parent);root.name=name;Stretch(root.GetComponent<RectTransform>());
        var body=root.transform.Find("Popup").GetComponent<RectTransform>();body.sizeDelta=size;
        var titleText=body.Find("Title").GetComponent<TMP_Text>();titleText.text=title;titleText.rectTransform.sizeDelta=new Vector2(460,60);
        var subText=body.Find("Description").GetComponent<TMP_Text>();subText.text=sub;subText.rectTransform.sizeDelta=new Vector2(528,36);
        root.GetComponent<PopupPresentation>().Configure(body,root.GetComponent<CanvasGroup>(),null);
        var dim=root.transform.Find("Dim").GetComponent<Image>();var button=dim.gameObject.AddComponent<Button>();button.targetGraphic=dim;button.transition=Selectable.Transition.None;button.navigation=new Navigation{mode=Navigation.Mode.None};
        return root;
    }
    private static GameObject Lobby()
    {
        var root=Rect("LobbyPanel",null,new Vector2(1920,1080));Stretch(root);var panel=root.gameObject.AddComponent<LobbyPanel>();
        var hero=Common("PaperCard",root);hero.name="HeroCard";var heroRect=hero.GetComponent<RectTransform>();heroRect.anchorMin=heroRect.anchorMax=new Vector2(0,0.5f);heroRect.pivot=new Vector2(0,0.5f);heroRect.anchoredPosition=new Vector2(120,-30);heroRect.sizeDelta=new Vector2(900,820);hero.GetComponent<PaperCardPresentation>().SetEmphasis(true,false);
        var board=MakeImage("BoardPreview",hero.transform,Sprite("lobby_board_preview"),Color.white);Center(board.rectTransform,450,226,new Vector2(420,425));board.preserveAspect=true;
        var title=Text("Title",hero.transform,"게임 입장",Jua,60,"3A2618");Center(title.rectTransform,450,486,new Vector2(800,80));
        var sub=Text("Description",hero.transform,"온라인 2~4인 · 직업을 골라 한 판",Medium,24,"7A5C3E");Center(sub.rectTransform,450,540,new Vector2(800,40));
        var quick=ActionButton("PrimaryButton","QuickJoin",hero.transform,180,584,540,104,"빠른 참가",44,true);
        var quickView=quick.GetComponent<ButtonPresentation>();quickView.SetSubtitle("열려 있는 공개방에 바로 들어가요");
        var quickLabel=quick.transform.Find("Label").GetComponent<TMP_Text>();
        quickLabel.rectTransform.pivot=new Vector2(0.5f,0.5f-4f/quickLabel.rectTransform.sizeDelta.y);
        quick.transform.Find("Subtitle").GetComponent<RectTransform>().pivot=new Vector2(0.5f,0.5f-1f/28f);
        var status=Text("Status",root,"",Medium,20,"7A5C3E");status.rectTransform.anchorMin=status.rectTransform.anchorMax=new Vector2(0,0);status.rectTransform.anchoredPosition=new Vector2(570,60);status.rectTransform.sizeDelta=new Vector2(860,32);
        Bind(panel,"quickJoinButton",quick);Bind(panel,"statusText",status);
        Bind(panel,"openCreateRoomButton",ActionButton("SecondaryButton","OpenCreate",hero.transform,180,716,260,72,"방 만들기",28,false));
        Bind(panel,"openJoinRoomButton",ActionButton("SecondaryButton","OpenJoin",hero.transform,460,716,260,72,"코드로 참가",28,false));
        var hidden=Rect("HiddenLegacy",root,Vector2.zero);hidden.gameObject.SetActive(false);
        var nickname=Common("InputField",hidden);nickname.name="NicknameInput";Bind(panel,"nicknameInput",nickname.GetComponent<TMP_InputField>());nickname.SetActive(false);
        foreach(var field in new[]{"mainMenuButton","settingsButton","collectionButton"})
        {var button=Rect(field,hidden,Vector2.zero).gameObject.AddComponent<Button>();Bind(panel,field,button);button.gameObject.SetActive(false);}
        var create=Window(root,"CreateRoomWindow",new Vector2(640,480),"방 만들기","친구를 불러 함께해요");var body=create.transform.Find("Popup");
        var visibilityLabel=Text("VisibilityLabel",body,"방 공개 설정",Bold,20,"7A5C3E");TopLeft(visibilityLabel.rectTransform,56,164,new Vector2(480,28));visibilityLabel.alignment=TextAlignmentOptions.MidlineLeft;
        var segment=Common("SegmentedControl",body);segment.name="Visibility";TopLeft(segment.GetComponent<RectTransform>(),56,200,new Vector2(528,80));
        var hint=Text("VisibilityHint",body,"",Medium,20,"A88E6E");Center(hint.rectTransform,320,312,new Vector2(540,28));
        var createStatus=Text("PopupStatus",body,"",Bold,19,"C8463F");Center(createStatus.rectTransform,320,336,new Vector2(540,24));
        Bind(panel,"publicRoomButton",segment.transform.Find("First").GetComponent<Button>());Bind(panel,"privateRoomButton",segment.transform.Find("Second").GetComponent<Button>());Bind(panel,"roomVisibilityText",hint);
        Bind(panel,"createRoomButton",ActionButton("PrimaryButton","Create",body,56,352,528,92,"방 만들기",38,true));
        Bind(panel,"createRoomWindow",create);Bind(panel,"closeCreateRoomButton",body.Find("CloseButton").GetComponentInChildren<Button>());Bind(panel,"createRoomDimButton",create.transform.Find("Dim").GetComponent<Button>());Bind(panel,"createRoomStatusText",createStatus);
        var join=Window(root,"JoinRoomWindow",new Vector2(640,460),"코드로 참가","방장에게 받은 방 코드를 입력하세요");body=join.transform.Find("Popup");
        var code=Common("InputField",body);code.name="CodeInput";TopLeft(code.GetComponent<RectTransform>(),56,160,new Vector2(528,96));
        var input=code.GetComponent<TMP_InputField>();input.textComponent.font=Jua;input.textComponent.fontSize=48;input.textComponent.alignment=TextAlignmentOptions.Center;input.textComponent.characterSpacing=6;
        ((TMP_Text)input.placeholder).text="방 코드";((TMP_Text)input.placeholder).alignment=TextAlignmentOptions.Center;
        var joinStatus=Text("PopupStatus",body,"",Bold,19,"C8463F");Center(joinStatus.rectTransform,320,290,new Vector2(540,30));
        Bind(panel,"roomCodeInput",input);Bind(panel,"joinRoomButton",ActionButton("PrimaryButton","Join",body,56,332,528,92,"참가하기",38,true));
        Bind(panel,"joinRoomWindow",join);Bind(panel,"closeJoinRoomButton",body.Find("CloseButton").GetComponentInChildren<Button>());Bind(panel,"joinRoomDimButton",join.transform.Find("Dim").GetComponent<Button>());Bind(panel,"joinRoomStatusText",joinStatus);
        create.SetActive(false);join.SetActive(false);return root.gameObject;
    }
    [MenuItem("Tools/Yutnori/UIv2/Generate Main Menu and Lobby")]
    public static void Generate()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {foreach(var pair in new Dictionary<string,Func<GameObject>>{{"MainMenuPanel",MainMenu},{"LobbyPanel",Lobby}})
            {var root=pair.Value();SceneManager.MoveGameObjectToScene(root,scene);PrefabUtility.SaveAsPrefabAsset(root,Folder+pair.Key+".prefab");UnityEngine.Object.DestroyImmediate(root);}}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    [MenuItem("Tools/Yutnori/UIv2/Replace Main Menu and Lobby References")]
    public static void ReplaceReferences()
    {
        var root=PrefabUtility.LoadPrefabContents(RootPath);var rows=new List<string>();
        try
        {
            var manager=root.GetComponentInChildren<UIManager>(true);var so=new SerializedObject(manager);var list=so.FindProperty("prefabs");
            var hierarchy=new Stack<string>();
            for(var item=manager.transform;item!=null;item=item.parent)hierarchy.Push(item.name);
            string managerPath=string.Join("/",hierarchy);
            int main=-1,lobby=-1;
            for(int i=0;i<list.arraySize;i++)
            {
                var old=list.GetArrayElementAtIndex(i).objectReferenceValue;
                if(old==null || (old.GetType()!=typeof(MainMenuPanel)&&old.GetType()!=typeof(LobbyPanel)))continue;
                if(old.GetType()==typeof(MainMenuPanel))main=i;else lobby=i;
                string path=Folder+old.GetType().Name+".prefab";rows.Add("| "+RootPath+" | "+managerPath+" · UIManager.prefabs["+i+"] | "+AssetDatabase.GetAssetPath(old)+" | "+path+" |");
                list.GetArrayElementAtIndex(i).objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent(old.GetType());
            }
            if(main<0||lobby<0)throw new InvalidOperationException("기존 메뉴 등록 없음");
            // 목록 순서는 기존대로 두고 실제 Open 순서로 메인 → Lobby 그리기 순서를 정한다.
            so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,RootPath);
            Directory.CreateDirectory(UIScreenPreparation.Reports);
            File.WriteAllText(UIScreenPreparation.Reports+"stage4_reference_changes.md","# 4단계 참조 교체\n\n| 파일 | 오브젝트·필드 | 이전 | 새 프리팹 |\n|---|---|---|---|\n"+string.Join("\n",rows)+"\n\n실제 그리기 순서는 LobbyController에서 MainMenuPanel을 먼저 Open하고 LobbyPanel을 다음에 Open하여 보장합니다. 같은 타입의 중복 등록은 추가하지 않았습니다.\n");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    [MenuItem("Tools/Yutnori/UIv2/Capture Main Menu and Entry Windows FHD")]
    public static void CaptureAll()
    {for(int state=0;state<3;state++)Capture(state);}
    private static void Capture(int state)
    {
        var scene=EditorSceneManager.NewPreviewScene();RenderTexture target=null;Texture2D image=null;var previous=RenderTexture.active;
        try
        {
            var cameraRoot=new GameObject("FHD Camera");SceneManager.MoveGameObjectToScene(cameraRoot,scene);var camera=cameraRoot.AddComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=540;
            var light=new GameObject("Directional Light");SceneManager.MoveGameObjectToScene(light,scene);light.AddComponent<Light>().type=LightType.Directional;
            var canvasRoot=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(canvasRoot,scene);var canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;canvasRoot.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
            var main=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"MainMenuPanel.prefab"),canvas.transform).GetComponent<MainMenuPanel>();main.Init();main.SetProfile("윷잘알","icon_002");main.SetGold(1250);
            var lobby=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"LobbyPanel.prefab"),canvas.transform).GetComponent<LobbyPanel>();lobby.Init();lobby.Open();lobby.SetNickname("윷잘알");
            if(state==1)lobby.OpenCreateRoomWindow();
            if(state==2){lobby.OpenJoinRoomWindow();var field=new SerializedObject(lobby).FindProperty("roomCodeInput");((TMP_InputField)field.objectReferenceValue).text="K7Q2MX";lobby.ShowStatus("방을 찾을 수 없습니다. 코드를 다시 확인해 주세요.");((TMP_InputField)field.objectReferenceValue).GetComponent<InputFocusPresentation>().SetFocused(true);}
            foreach(var view in canvasRoot.GetComponentsInChildren<ButtonPresentation>(true))view.Refresh();
            foreach(var pixel in canvasRoot.GetComponentsInChildren<PixelIconSampling>(true))pixel.Refresh();
            Canvas.ForceUpdateCanvases();
            foreach(var text in canvasRoot.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate(true);
            target=new RenderTexture(1920,1080,24);target.Create();camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
            var file=new[]{"stage4_mainmenu_fhd.png","stage4_create_room_fhd.png","stage4_join_error_fhd.png"}[state];Directory.CreateDirectory(UIScreenPreparation.Reports);File.WriteAllBytes(UIScreenPreparation.Reports+file,image.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;if(image!=null)UnityEngine.Object.DestroyImmediate(image);ReleaseTarget(scene,target);EditorSceneManager.ClosePreviewScene(scene);}
    }
    [MenuItem("Tools/Yutnori/UIv2/Validate Main Menu and Lobby")]
    public static string Validate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.Object.FindFirstObjectByType<LobbyNetworkCoordinator>()!=null)
            throw new InvalidOperationException("Play를 종료하고 Login 씬에서 검증해 주세요.");
        var original=SceneManager.GetActiveScene();
        var source=EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity",OpenSceneMode.Additive);
        UIBase[] assets;
        try
        {
            var sourceManager=source.GetRootGameObjects().SelectMany(item=>item.GetComponentsInChildren<UIManager>(true)).Single();
            var list=new SerializedObject(sourceManager).FindProperty("prefabs");assets=new UIBase[list.arraySize];
            for(int i=0;i<assets.Length;i++)assets[i]=(UIBase)list.GetArrayElementAtIndex(i).objectReferenceValue;
        }
        finally{EditorSceneManager.CloseScene(source,true);SceneManager.SetActiveScene(original);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var results=new List<string>();RenderTexture target=null;
        try
        {
            SceneManager.SetActiveScene(scene);
            var actor=new GameObject("UI-only coordinator").AddComponent<LobbyNetworkCoordinator>();
            // 읽기만 하는 별도 identity를 메모리에서 채운다. 토큰·PlayerPrefs·API 객체를 만들지 않는다.
            var identity=new PlayerIdentity("UIv2ReadOnlyValidation.");
            SetPrivate(identity,"<Nickname>k__BackingField","테스트닉네임");SetPrivate(identity,"<Gold>k__BackingField",1000);
            SetPrivate(identity,"<Token>k__BackingField",string.Empty);SetPrivate(identity,"<PlayerId>k__BackingField","preview-player");
            SetPrivate(actor,"playerIdentity",identity);SetPrivate(actor,"<LocalIconId>k__BackingField","icon_002");
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=540;
            target=new RenderTexture(1920,1080,24);target.Create();camera.targetTexture=target;
            new GameObject("Directional Light").AddComponent<Light>().type=LightType.Directional;
            var canvasObject=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;canvasObject.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
            var panelRoot=Rect("Panels",canvas.transform,Vector2.zero);Stretch(panelRoot);
            var popupRoot=Rect("Popups",canvas.transform,Vector2.zero);Stretch(popupRoot);
            var manager=canvasObject.AddComponent<UIManager>();var serialized=new SerializedObject(manager);var entries=serialized.FindProperty("prefabs");entries.arraySize=assets.Length;
            for(int i=0;i<assets.Length;i++)entries.GetArrayElementAtIndex(i).objectReferenceValue=assets[i];serialized.ApplyModifiedPropertiesWithoutUndo();Bind(manager,"panelRoot",panelRoot);Bind(manager,"popupRoot",popupRoot);
            CallPrivate(manager,"Awake");
            var eventSystem=new GameObject("EventSystem",typeof(EventSystem)).GetComponent<EventSystem>();
            var controller=new GameObject("Controller").AddComponent<LobbyController>();Bind(controller,"uiManager",manager);CallPrivate(controller,"Start");
            var main=manager.Get<MainMenuPanel>();var lobby=manager.Get<LobbyPanel>();
            Require(main.IsOpen&&lobby.IsOpen&&main.transform.GetSiblingIndex()<lobby.transform.GetSiblingIndex(),"Paired opening/draw order");
            Require(lobby.GetComponent<Graphic>()==null,"Lobby root no graphic");
            Require(Field<TMP_InputField>(lobby,"nicknameInput").gameObject.activeSelf==false,"Hidden nickname kept");
            results.Add("Controller Start opens Main then Lobby; blank Lobby root: PASS");
            var nickname=Field<TMP_Text>(main,"nicknameText");var gold=Field<TMP_Text>(main,"goldText");var portrait=Field<Image>(main,"profileIcon");
            Require(nickname.text=="테스트닉네임"&&gold.text==1000.ToString("N0"),"Immediate cached display");
            main.ShowStatus("keep-status");main.Open();Require(Field<TMP_Text>(main,"statusText").text=="keep-status","Silent profile fetch failure");
            main.SetProfile("테스트닉네임","");Require(portrait.sprite.name=="profile_px_locked","Empty icon fallback");
            main.SetProfile("테스트닉네임","missing_icon");Require(portrait.sprite.name=="profile_px_locked","Missing icon fallback");
            SetPrivate(identity,"<Gold>k__BackingField",2200);SetPrivate(identity,"<Nickname>k__BackingField","새닉네임");
            var displayEvent=(Action)GetPrivate(actor,"LocalDisplayChanged");Require(displayEvent!=null&&displayEvent.GetInvocationList().Length==1,"Single display subscription");displayEvent.Invoke();
            Require(gold.text==2200.ToString("N0")&&nickname.text=="새닉네임","Display event refresh");
            main.Close();Require(GetPrivate(actor,"LocalDisplayChanged")==null,"Closed display unsubscribe");main.Open();
            Require(((Action)GetPrivate(actor,"LocalDisplayChanged")).GetInvocationList().Length==1,"Reopen single subscription");
            results.Add("Cache immediately; silent query failure; profile/gold event; fallback; unsubscribe/reopen: PASS");
            // 개별 MainMenu 재열기 테스트 뒤 실제 Controller의 Main → Lobby 순서를 복원한다.
            lobby.Open();
            Canvas.ForceUpdateCanvases();camera.Render();
            var card=Field<Button>(main,"profileButton");var pointer=new PointerEventData(eventSystem);
            pointer.position=camera.WorldToScreenPoint(card.GetComponent<RectTransform>().TransformPoint(card.GetComponent<RectTransform>().rect.center));
            var hits=new List<RaycastResult>();var raycaster=canvasObject.GetComponent<GraphicRaycaster>();raycaster.Raycast(pointer,hits);
            Require(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==card,"Menu click passes Lobby");
            lobby.OpenJoinRoomWindow();Canvas.ForceUpdateCanvases();camera.Render();hits.Clear();raycaster.Raycast(pointer,hits);
            var joinWindow=Field<GameObject>(lobby,"joinRoomWindow");Require(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(joinWindow.transform),"Modal blocks background menu");
            var input=Field<TMP_InputField>(lobby,"roomCodeInput");input.text="";Require(!Field<Button>(lobby,"joinRoomButton").interactable,"Empty join disabled");
            int joins=0;string joinName=null,joinCode=null;controller.JoinRoomRequested+=(name,code)=>{joins++;joinName=name;joinCode=code;};
            input.text=" ab12cd ";input.onSubmit.Invoke(input.text);
            Require(joins==1&&joinName=="테스트닉네임"&&joinCode=="AB12CD","Enter and account nickname");
            Require(!input.interactable&&!Field<Button>(lobby,"closeJoinRoomButton").interactable&&!Field<Button>(lobby,"joinRoomDimButton").interactable,"Busy controls disabled");
            Field<Button>(lobby,"joinRoomDimButton").onClick.Invoke();Require(joinWindow.activeSelf,"Busy cannot dismiss");
            controller.ReceiveError("참가 실패 테스트");Require(joinWindow.activeSelf&&Field<TMP_Text>(lobby,"joinRoomStatusText").text=="참가 실패 테스트","Failure remains in window");
            Field<Button>(lobby,"closeJoinRoomButton").onClick.Invoke();Require(!joinWindow.activeSelf,"Join close");lobby.OpenJoinRoomWindow();Require(Field<TMP_Text>(lobby,"joinRoomStatusText").text=="","Reopen status cleared");
            results.Add("GraphicRaycaster click-through/modal block; empty input; Enter; busy; failure/close/reopen: PASS");
            lobby.SetBusy(false);lobby.OpenCreateRoomWindow();Require(!joinWindow.activeSelf,"Only one window");
            int creates=0;bool visibility=true;string createName=null;controller.CreateRoomRequested+=name=>{creates++;visibility=controller.IsPublicRoom;createName=name;};
            Field<Button>(lobby,"publicRoomButton").onClick.Invoke();Field<Button>(lobby,"createRoomButton").onClick.Invoke();Require(creates==1&&visibility&&createName=="테스트닉네임","Public create input");
            controller.ReceiveError("생성 실패 테스트");Require(Field<TMP_Text>(lobby,"createRoomStatusText").text=="생성 실패 테스트","Create window error");
            Field<Button>(lobby,"privateRoomButton").onClick.Invoke();Field<Button>(lobby,"createRoomButton").onClick.Invoke();Require(creates==2&&!visibility,"Private create input");
            controller.ReceiveError("");Field<Button>(lobby,"createRoomDimButton").onClick.Invoke();Require(!Field<GameObject>(lobby,"createRoomWindow").activeSelf,"Dim close");
            int quick=0;controller.QuickJoinRequested+=name=>{if(name=="테스트닉네임")quick++;};Field<Button>(lobby,"quickJoinButton").onClick.Invoke();Require(quick==1,"Quick join account input");controller.ReceiveError("");
            controller.SetLocalPlayer("preview-player","테스트닉네임");controller.ReceiveRoomState(new RoomStateMessage{roomCode="AB12CD",status="WAITING",currentPlayers=1,maxPlayers=4,members=new List<RoomPlayer>{new RoomPlayer{playerId="preview-player",nickname="테스트닉네임",team=1}}});
            Require(!main.IsOpen&&!lobby.IsOpen&&manager.IsOpen<RoomPanel>(),"Room state closes pair");controller.ReturnToLobby();
            Require(main.IsOpen&&lobby.IsOpen&&!manager.IsOpen<RoomPanel>()&&main.transform.GetSiblingIndex()<lobby.transform.GetSiblingIndex(),"Leave restores pair");
            lobby.OpenCreateRoomWindow();lobby.Close();lobby.Open();Require(!Field<GameObject>(lobby,"createRoomWindow").activeSelf&&!joinWindow.activeSelf,"Panel reopened windows closed");
            controller.ShowReconnectFailure("복귀 실패 테스트");Require(main.IsOpen&&lobby.IsOpen,"Reconnect failure restores pair");
            results.Add("Public/private/quick event inputs; simulated ROOM_STATE/leave/reconnect restore pair: PASS");
            Field<Button>(main,"gachaButton").onClick.Invoke();Require(manager.IsOpen<GachaPanel>(),"Gacha route");manager.Close<GachaPanel>();
            Field<Button>(main,"collectionButton").onClick.Invoke();Require(manager.IsOpen<CollectionPanel>(),"Augment route");manager.Close<CollectionPanel>();
            Field<Button>(main,"achievementButton").onClick.Invoke();Require(manager.IsOpen<AchievementPanel>(),"Achievement route");manager.Close<AchievementPanel>();
            Field<Button>(main,"profileButton").onClick.Invoke();Require(manager.IsOpen<ProfilePanel>(),"Profile route");manager.Close<ProfilePanel>();
            Field<Button>(main,"settingsButton").onClick.Invoke();Require(manager.IsOpen<SettingsPanel>(),"Settings route");manager.Close<SettingsPanel>();
            Require(main.transform.Find("ProfileChip").GetComponent<Button>()==null,"No optional profile chip button");
            foreach(var badge in main.GetComponentsInChildren<Transform>(true).Where(item=>item.name=="NewBadge"))Require(!badge.gameObject.activeSelf,"Badge hidden");
            results.Add("Existing five menu routes; optional chip click absent; NewBadge hidden: PASS");
            main.Open();CallPrivate(main,"OnDestroy");UnityEngine.Object.DestroyImmediate(main.gameObject);Require(GetPrivate(actor,"LocalDisplayChanged")==null,"Destroyed display unsubscribe");
            results.Add("OnDestroy handler unsubscribes display event (explicit Edit lifecycle call): PASS");
        }
        finally
        {
            ReleaseTarget(scene,target);
            SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(scene,true);
        }
        string report=string.Join("\n",results)+"\n";Directory.CreateDirectory(UIScreenPreparation.Reports);File.WriteAllText(UIScreenPreparation.Reports+"stage4_validation.txt",report);Debug.Log(report);return report;
    }
    private static T Field<T>(UnityEngine.Object owner,string name) where T:UnityEngine.Object
    {return (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;}
    private static object GetPrivate(object owner,string name)
    {return owner.GetType().GetField(name,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(owner);}
    private static void SetPrivate(object owner,string name,object value)
    {owner.GetType().GetField(name,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(owner,value);}
    private static void CallPrivate(object owner,string name)
    {owner.GetType().GetMethod(name,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(owner,null);}
    private static void Require(bool condition,string name)
    {if(!condition)throw new InvalidOperationException("[Outgame validation] "+name);}

    private static void ReleaseTarget(Scene scene,RenderTexture target)
    {
        if(target==null)return;
        foreach(var camera in scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Camera>(true)))
            if(camera.targetTexture==target)camera.targetTexture=null;
        target.Release();UnityEngine.Object.DestroyImmediate(target);
    }

}
