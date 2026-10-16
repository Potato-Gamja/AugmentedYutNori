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

/// <summary>명세 1·2장만 생성하고 Login 씬의 기존 등록 두 개만 교체한다.</summary>
public static class UILoginPrefabBuilder
{
    public const string Panels = "Assets/Prefabs/UIv2/Panels/";
    private const string LoginScene = "Assets/Scenes/Login.unity";
    private static TMP_FontAsset Jua => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.Jua);
    private static TMP_FontAsset Medium => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.MediumDynamic);
    private static TMP_FontAsset Bold => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.Bold);
    private static Color ColorOf(string value)
    {
        Color color; ColorUtility.TryParseHtmlString("#" + value, out color); return color;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.sizeDelta = size; return rect;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static void TopLeft(RectTransform rect, float x, float y, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
        rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = size;
    }
    private static void Center(RectTransform rect, float x, float y, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0.5f,0.5f);
        rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = size;
    }
    private static Sprite Sprite(string name)
    {
        string path = AssetDatabase.FindAssets(name + " t:Texture2D", new[] { "Assets/Art" })
            .Select(AssetDatabase.GUIDToAssetPath).Single(item => Path.GetFileNameWithoutExtension(item) == name);
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
    }
    private static Image Image(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
    {
        var rect = Rect(name, parent, Vector2.zero); Stretch(rect);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = color; image.raycastTarget = raycast; return image;
    }
    private static TMP_Text Text(string name, Transform parent, string value, TMP_FontAsset font, float size, string color)
    {
        var rect = Rect(name, parent, new Vector2(540,40));
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.fontStyle = FontStyles.Normal;
        text.text = value; text.color = ColorOf(color); text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
    private static GameObject Common(string name, Transform parent)
    {
        return (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(UICommonPrefabBuilder.Folder + name + ".prefab"), parent);
    }
    private static void Bind(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target); var property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException("필드 없음: " + target.GetType().Name + "." + field);
        property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static TMP_Text VisualBackground(RectTransform root)
    {
        var background = Image("Background", root, Sprite("bg_hanji_tile"), Color.white);
        background.type = UnityEngine.UI.Image.Type.Tiled;
        var mark = Image("LogoMark", root, Sprite("logo_mark"), Color.white);
        Center(mark.rectTransform,960,236,new Vector2(260,210)); mark.preserveAspect = true;
        var title = Image("LogoTitle", root, Sprite("logo_title"), Color.white);
        Center(title.rectTransform,960,414,new Vector2(354,160)); title.preserveAspect = true;
        var subtitle = Text("Subtitle",root,"윷 한 판에 팔자가 바뀐다",Medium,30,"7A5C3E");
        Center(subtitle.rectTransform,960,494,new Vector2(800,44));
        var footer = Text("Footer",root,"이용약관 · 개인정보 처리방침",Medium,18,"A88E6E");
        footer.rectTransform.anchorMin = footer.rectTransform.anchorMax = new Vector2(0.5f,0);
        footer.rectTransform.anchoredPosition = new Vector2(0,50); footer.rectTransform.sizeDelta = new Vector2(800,30);
        var version = Text("Version",root,"v"+Application.version,Medium,16,"A88E6E");
        version.rectTransform.anchorMin = version.rectTransform.anchorMax = new Vector2(1,0);
        version.rectTransform.pivot = new Vector2(1,0.5f); version.rectTransform.anchoredPosition = new Vector2(-30,40);
        version.rectTransform.sizeDelta = new Vector2(240,30); version.alignment = TextAlignmentOptions.MidlineRight;
        Decoration(root,"Tiger","tiger",540,700,190,"D6403C",0);
        Decoration(root,"Rabbit","rabbit",1380,700,190,"2E6BD1",0.7f);
        Decoration(root,"Dragon","dragon",430,900,150,"2A9460",1.5f);
        Decoration(root,"Cat","cat",1490,900,150,"E0950F",2.3f);
        return version;
    }
    private static GameObject Login()
    {
        var root = Rect("LoginPanel", null, new Vector2(1920,1080)); Stretch(root);
        var panel = root.gameObject.AddComponent<LoginPanel>();
        var version = VisualBackground(root);
        var card = Common("PaperCard",root); card.name = "LoginCard";
        TopLeft(card.GetComponent<RectTransform>(),660,580,new Vector2(600,300));

        // 공식 Google 에셋을 넣을 자리만 준비한다. G 로고를 임의로 만들지 않는다.
        var googleRoot = Rect("GoogleButton",card.transform,new Vector2(480,76));
        TopLeft(googleRoot,60,48,new Vector2(480,76));
        var googleGroup = googleRoot.gameObject.AddComponent<CanvasGroup>();
        var placeholder = Image("PlaceholderFace",googleRoot,Sprite("btn_rethrow_normal"),Color.white,true);
        placeholder.type = UnityEngine.UI.Image.Type.Sliced;
        var googleBorder = Image("PlaceholderBorder",googleRoot,Sprite("btn_rethrow_border"),ColorOf("A8A8A8"));
        googleBorder.type = UnityEngine.UI.Image.Type.Sliced;
        var googleLabel = Text("PlaceholderLabel",googleRoot,"Google 계정으로 로그인",Medium,26,"242424");
        Center(googleLabel.rectTransform,240,38,new Vector2(440,44));
        var google = googleRoot.gameObject.AddComponent<Button>(); google.targetGraphic = placeholder;
        google.navigation = new Navigation { mode = Navigation.Mode.None };
        google.transition = Selectable.Transition.ColorTint;
        var colors = google.colors; colors.disabledColor = Color.white; google.colors = colors;

        var resume = Common("SecondaryButton",card.transform); resume.name = "ContinueButton";
        TopLeft(resume.GetComponent<RectTransform>(),60,148,new Vector2(480,76));
        var resumeButton = resume.GetComponentInChildren<Button>();
        var resumeLabel = resume.transform.Find("Button/Label").GetComponent<TMP_Text>();
        resumeLabel.fontSize = 30; resumeLabel.enableAutoSizing = true;
        resumeLabel.fontSizeMin = 16; resumeLabel.fontSizeMax = 30;
        resumeLabel.text = "이전 로그인으로 계속하기";
        resumeButton.interactable = false;
        var cancel = Common("SecondaryButton",card.transform); cancel.name = "CancelButton";
        TopLeft(cancel.GetComponent<RectTransform>(),60,148,new Vector2(480,76));
        var cancelButton = cancel.GetComponentInChildren<Button>();
        cancel.transform.Find("Button/Label").GetComponent<TMP_Text>().fontSize = 30;
        cancel.transform.Find("Button/Label").GetComponent<TMP_Text>().text = "취소";
        cancelButton.gameObject.SetActive(false);
        var status = Text("Status",card.transform,"",Medium,19,"7A5C3E");
        Center(status.rectTransform,300,262,new Vector2(540,30));
        status.enableAutoSizing = true; status.fontSizeMin = 16; status.fontSizeMax = 19;
        Bind(panel,"googleLoginButton",google); Bind(panel,"continueButton",resumeButton);
        Bind(panel,"continueLabel",resumeLabel); Bind(panel,"cancelButton",cancelButton); Bind(panel,"statusText",status);
        root.gameObject.AddComponent<LoginPanelPresentation>().Configure(google,googleGroup,resumeButton,
            resume.transform.Find("Button/Border").GetComponent<Image>(),resumeLabel,status,version);
        UIAccountButtonsBuilder.ConfigureLogin(root.gameObject);
        return root.gameObject;
    }
    private static void Decoration(Transform parent,string name,string key,float x,float y,float size,string tint,float phase)
    {
        var root = Rect(name,parent,new Vector2(size,size)); Center(root,x,y,new Vector2(size,size));
        CroppedSprite("Rim",root,Sprite("skin_"+key+"_rim"),ColorOf(tint),size);
        CroppedSprite("Body",root,Sprite("skin_"+key),Color.white,size);
        root.gameObject.AddComponent<DecorativeFloat>().Configure(phase);
    }
    private static void CroppedSprite(string name, Transform parent, Sprite sprite, Color color, float size)
    {
        var image = Image(name,parent,sprite,color);
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f,0.5f);
        // 잘린 Sprite Rect를 원래 PNG 좌표계로 되돌려 몸통과 테두리 비율을 보존한다.
        var scale = new Vector2(size/sprite.texture.width,size/sprite.texture.height);
        rect.sizeDelta = Vector2.Scale(sprite.rect.size,scale);
        rect.anchoredPosition = Vector2.Scale(sprite.rect.center -
            new Vector2(sprite.texture.width,sprite.texture.height)*0.5f,scale);
    }
    private static GameObject Nickname()
    {
        var root = Rect("NicknamePanel",null,new Vector2(1920,1080)); Stretch(root);
        var panel = root.gameObject.AddComponent<NicknamePanel>();
        var version = VisualBackground(root);
        version.gameObject.AddComponent<ApplicationVersionLabel>().Configure(version);
        Image("Dim",root,null,new Color(58/255f,38/255f,24/255f,0.59f),true);
        // 첫 설정에 닫기 버튼을 추가하지 않고 기존 OnOpen 입력 포커스를 유지한다.
        var paper = Common("PaperCard",root); paper.name = "Popup";
        paper.SetActive(true);
        var body = paper.GetComponent<RectTransform>();
        body.anchorMin = body.anchorMax = new Vector2(0.5f,0.5f); body.anchoredPosition = new Vector2(0,15);
        body.sizeDelta = new Vector2(640,470); paper.transform.Find("Paper").GetComponent<Image>().raycastTarget = true;
        var title = Text("Title",body,"닉네임 설정",Jua,46,"3A2618"); Center(title.rectTransform,320,70,new Vector2(560,60));
        var sub = Text("Description",body,"게임에서 사용할 닉네임을 정해 주세요",Medium,22,"7A5C3E");
        Center(sub.rectTransform,320,124,new Vector2(560,36));
        var inputRoot = Common("InputField",body); inputRoot.name = "Input";
        TopLeft(inputRoot.GetComponent<RectTransform>(),60,170,new Vector2(520,76));
        var input = inputRoot.GetComponent<TMP_InputField>();
        ((TMP_Text)input.placeholder).text = "닉네임 (2~20자)";
        var rule = Text("Rule",body,"2~20자",Medium,18,"A88E6E");
        TopLeft(rule.rectTransform,80,260,new Vector2(300,24)); rule.alignment = TextAlignmentOptions.MidlineLeft;
        var status = Text("Status",body,"",Bold,19,"C8463F"); Center(status.rectTransform,320,316,new Vector2(540,32));
        var confirm = Common("PrimaryButton",body); confirm.name = "Confirm";
        TopLeft(confirm.GetComponent<RectTransform>(),60,356,new Vector2(520,84));
        var button = confirm.GetComponentInChildren<Button>();
        Bind(panel,"nicknameInput",input); Bind(panel,"confirmButton",button); Bind(panel,"statusText",status);
        return root.gameObject;
    }

    [MenuItem("Tools/Yutnori/UIv2/Generate Login and Nickname Prefabs")]
    public static void Generate()
    {
        Directory.CreateDirectory(Panels); AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach(var pair in new Dictionary<string,Func<GameObject>> { {"LoginPanel",Login},{"NicknamePanel",Nickname} })
            {
                var root = pair.Value(); SceneManager.MoveGameObjectToScene(root,scene);
                PrefabUtility.SaveAsPrefabAsset(root,Panels+pair.Key+".prefab"); UnityEngine.Object.DestroyImmediate(root);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [MenuItem("Tools/Yutnori/UIv2/Replace Login Scene Panel References")]
    public static void ReplaceReferences()
    {
        var original = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(LoginScene); bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(LoginScene,OpenSceneMode.Additive);
        var rows = new List<string>();
        try
        {
            var manager = scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<UIManager>(true)).Single();
            var serialized = new SerializedObject(manager); var entries = serialized.FindProperty("prefabs");
            foreach (var type in new[]{typeof(LoginPanel),typeof(NicknamePanel)})
            {
                int count = 0;
                for (int i=0;i<entries.arraySize;i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i); var old = entry.objectReferenceValue;
                    if (old == null || old.GetType() != type) continue;
                    count++;
                    string oldPath = AssetDatabase.GetAssetPath(old);
                    var replacement = AssetDatabase.LoadAssetAtPath<GameObject>(Panels+type.Name+".prefab").GetComponent(type);
                    entry.objectReferenceValue = replacement;
                    rows.Add("| "+LoginScene+" | UIRoot/UIManager · prefabs["+i+"] | "+oldPath+" | "+AssetDatabase.GetAssetPath(replacement)+" |");
                }
                if(count!=1) throw new InvalidOperationException(type.Name+" 등록 수: "+count);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory(UIScreenPreparation.Reports);
            File.WriteAllText(UIScreenPreparation.Reports+"stage3_reference_changes.md",
                "# 로그인 참조 교체\n\n| 파일 | 오브젝트·필드 | 이전 프리팹 | 새 프리팹 |\n|---|---|---|---|\n"+
                string.Join("\n",rows)+"\n\n공통 Assets/Prefabs/UI/UIRoot.prefab에는 두 타입의 기존 등록이 없습니다. Login 씬의 UIManager 목록 override만 교체했습니다. 공통 UIRoot 및 옛 패널 프리팹은 유지합니다.\n");
        }
        finally { if(opened)EditorSceneManager.CloseScene(scene,true); SceneManager.SetActiveScene(original); }
    }

    [MenuItem("Tools/Yutnori/UIv2/Capture Login and Nickname FHD")]
    public static void CaptureAll()
    {
        for(int state=0;state<4;state++) Capture(state);
    }
    private static void Capture(int state)
    {
        var scene = EditorSceneManager.NewPreviewScene(); RenderTexture target = null; Texture2D image = null;
        var previous = RenderTexture.active;
        try
        {
            var cameraRoot = new GameObject("Capture Camera"); SceneManager.MoveGameObjectToScene(cameraRoot,scene);
            var camera = cameraRoot.AddComponent<Camera>(); camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.transform.position = new Vector3(0,0,-10); camera.orthographic = true; camera.orthographicSize = 540;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = ColorOf("F3E6CF");
            var light = new GameObject("Directional Light"); SceneManager.MoveGameObjectToScene(light,scene); light.AddComponent<Light>().type=LightType.Directional;
            var canvasRoot = new GameObject("Capture Canvas",typeof(RectTransform),typeof(Canvas)); SceneManager.MoveGameObjectToScene(canvasRoot,scene);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            canvasRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(1920,1080);
            var login = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Panels+"LoginPanel.prefab"),canvas.transform).GetComponent<LoginPanel>();
            login.Init(); login.Open();
            login.SetContinueAvailable(state!=0,"윷잘알"); login.SetBusy(state==2); login.SetCancelable(state==2);
            login.ShowStatus(state==0?"처음이라면 Google 계정으로 로그인해 주세요":state==2?"브라우저에서 Google 로그인을 마무리해 주세요":"");
            foreach(var view in login.GetComponentsInChildren<ButtonPresentation>(true))view.Refresh();
            login.GetComponent<LoginPanelPresentation>().Refresh();
            if(state==3)
            {
                login.Close();
                var nickname = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Panels+"NicknamePanel.prefab"),canvas.transform).GetComponent<NicknamePanel>();
                nickname.Init(); nickname.Open(); nickname.SetNickname("윷잘알"); nickname.ShowStatus("이미 사용 중인 닉네임이에요");
                nickname.GetComponentInChildren<InputFocusPresentation>().SetFocused(true);
            }
            Canvas.ForceUpdateCanvases();
            foreach(var text in canvasRoot.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate(true);
            target = new RenderTexture(1920,1080,24); target.Create(); camera.targetTexture=target; camera.Render();
            RenderTexture.active=target; image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
            string file = new[]{"stage3_login_no_saved_fhd.png","stage3_login_saved_fhd.png","stage3_login_busy_fhd.png","stage3_nickname_error_fhd.png"}[state];
            Directory.CreateDirectory(UIScreenPreparation.Reports); File.WriteAllBytes(UIScreenPreparation.Reports+file,image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;
            if(image!=null)UnityEngine.Object.DestroyImmediate(image);
            if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    [MenuItem("Tools/Yutnori/UIv2/Validate Login and Nickname")]
    public static string Validate()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var results = new List<string>();
        try
        {
            var eventObject = new GameObject("Validation EventSystem",typeof(EventSystem));
            SceneManager.MoveGameObjectToScene(eventObject,scene);
            var loginRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Panels+"LoginPanel.prefab"));
            SceneManager.MoveGameObjectToScene(loginRoot,scene);
            var login = loginRoot.GetComponent<LoginPanel>(); login.Init();
            foreach(var name in new[]{"googleLoginButton","continueButton","continueLabel","cancelButton","statusText"})
                Require(new SerializedObject(login).FindProperty(name).objectReferenceValue!=null,"Login binding "+name);
            var google = (Button)new SerializedObject(login).FindProperty("googleLoginButton").objectReferenceValue;
            var resume = (Button)new SerializedObject(login).FindProperty("continueButton").objectReferenceValue;
            var cancel = (Button)new SerializedObject(login).FindProperty("cancelButton").objectReferenceValue;
            var label = (TMP_Text)new SerializedObject(login).FindProperty("continueLabel").objectReferenceValue;
            var status = (TMP_Text)new SerializedObject(login).FindProperty("statusText").objectReferenceValue;
            var presentation = login.GetComponent<LoginPanelPresentation>();
            login.SetBusy(false); login.SetContinueAvailable(false,null); login.SetCancelable(false);
            login.ShowStatus("처음이라면 Google 계정으로 로그인해 주세요"); RefreshButtons(login); presentation.Refresh();
            Require(google.interactable && !resume.interactable && resume.gameObject.activeSelf && !cancel.gameObject.activeSelf,"No saved login state");
            Require(label.text=="이전 로그인으로 계속하기" && label.color==ColorOf("B9AC97"),"No saved label");
            Require(resume.transform.Find("Border").GetComponent<Image>().color==ColorOf("DCC6A0"),"No saved border");
            results.Add("Login no saved state: PASS");
            login.SetContinueAvailable(true,"윷잘알"); login.ShowStatus(""); RefreshButtons(login); presentation.Refresh();
            Require(resume.interactable && label.text=="윷잘알(으)로 계속하기" && !status.gameObject.activeSelf,"Saved login state");
            results.Add("Login saved state and empty status: PASS");
            int down=0,click=0,resumed=0,canceled=0;
            login.GoogleLoginPressed+=()=>down++; login.GoogleLoginRequested+=()=>click++;
            login.ContinueRequested+=()=>resumed++; login.CancelRequested+=()=>canceled++;
            var data = new PointerEventData(eventObject.GetComponent<EventSystem>()) { button=PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(google.gameObject,data,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(google.gameObject,data,ExecuteEvents.pointerClickHandler);
            resume.onClick.Invoke();
            Require(down==1 && click==1 && resumed==1,"Existing Google PointerDown/Click and Continue events");
            login.SetBusy(true); login.SetCancelable(true); login.ShowStatus("로그인 중입니다"); RefreshButtons(login); presentation.Refresh();
            Require(!google.interactable && !resume.gameObject.activeSelf && cancel.gameObject.activeSelf,"Busy login state");
            ExecuteEvents.Execute(google.gameObject,data,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(google.gameObject,data,ExecuteEvents.pointerClickHandler);
            cancel.onClick.Invoke();
            Require(down==1 && click==1 && canceled==1,"Busy Google blocking and Cancel event");
            results.Add("Login busy state and existing events: PASS");
            login.SetBusy(false); login.SetCancelable(false); login.ShowStatus(""); presentation.Refresh();
            Require(google.interactable && resume.interactable && resume.gameObject.activeSelf && !cancel.gameObject.activeSelf,"Login restored state");
            results.Add("Login cancel/restored state: PASS");
            Require(login.GetComponentsInChildren<DecorativeFloat>(true).Length==4,"Decorations count");
            foreach(var image in login.GetComponentsInChildren<DecorativeFloat>(true).SelectMany(item=>item.GetComponentsInChildren<Image>()))
            {
                Require(!image.raycastTarget,"Decoration raycast disabled");
                var size = image.transform.parent.GetComponent<RectTransform>().sizeDelta;
                var scale = new Vector2(size.x/image.sprite.texture.width,size.y/image.sprite.texture.height);
                Require(image.rectTransform.sizeDelta==Vector2.Scale(image.sprite.rect.size,scale),"Cropped sprite size");
                Require(image.rectTransform.anchoredPosition==Vector2.Scale(image.sprite.rect.center-
                    new Vector2(image.sprite.texture.width,image.sprite.texture.height)*0.5f,scale),"Cropped sprite offset");
            }
            results.Add("Decorations crop alignment and no raycast: PASS");
            var nicknameRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Panels+"NicknamePanel.prefab"));
            SceneManager.MoveGameObjectToScene(nicknameRoot,scene);
            var nickname = nicknameRoot.GetComponent<NicknamePanel>(); nickname.Init();
            foreach(var name in new[]{"nicknameInput","confirmButton","statusText"})
                Require(new SerializedObject(nickname).FindProperty(name).objectReferenceValue!=null,"Nickname binding "+name);
            var input = (TMP_InputField)new SerializedObject(nickname).FindProperty("nicknameInput").objectReferenceValue;
            var confirm = (Button)new SerializedObject(nickname).FindProperty("confirmButton").objectReferenceValue;
            var error = (TMP_Text)new SerializedObject(nickname).FindProperty("statusText").objectReferenceValue;
            Require(input.characterLimit==NicknamePanel.MaxLength,"Nickname length limit");
            int submitted=0; string submittedName=null; nickname.Submitted+=value=>{submitted++;submittedName=value;};
            nickname.SetNickname("a"); confirm.onClick.Invoke();
            Require(submitted==0 && error.text==NicknamePanel.Validate("a"),"Nickname validation event");
            nickname.SetNickname("윷잘알"); confirm.onClick.Invoke();
            Require(submitted==1 && submittedName=="윷잘알","Nickname submit event");
            nickname.SetBusy(true); confirm.onClick.Invoke();
            Require(submitted==1 && !confirm.interactable && !input.interactable,"Nickname busy block");
            Require(error.font==Bold && error.color==ColorOf("C8463F"),"Nickname error style");
            Require(nickname.GetComponentsInChildren<Button>(true).Length==1 && nicknameRoot.transform.Find("Popup/Counter")==null,"No close or optional counter");
            Require(nicknameRoot.transform.Find("LogoMark")!=null && nicknameRoot.transform.Find("Dim").GetComponent<Image>().raycastTarget,"Nickname background and dim");
            results.Add("Nickname bindings/validation/submit/busy/error/background: PASS");
            results.Add("No optional counter or close button: PASS");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        string report=string.Join("\n",results)+"\n";
        Directory.CreateDirectory(UIScreenPreparation.Reports);
        File.WriteAllText(UIScreenPreparation.Reports+"stage3_validation.txt",report); Debug.Log(report); return report;
    }
    private static void RefreshButtons(Component root)
    {
        foreach(var view in root.GetComponentsInChildren<ButtonPresentation>(true))view.Refresh();
    }
    private static void Require(bool condition,string message)
    {
        if(!condition)throw new InvalidOperationException("[Login UI validation] "+message);
    }

}
