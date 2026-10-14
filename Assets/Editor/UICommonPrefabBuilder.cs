using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YutNori.UI.Common;

/// <summary>명세 0-3~0-5 공통 부품만 재생성한다. 기존 패널/씬 참조는 변경하지 않는다.</summary>
public static class UICommonPrefabBuilder
{
    public const string Folder = "Assets/Prefabs/UIv2/Common/";
    public const string PreviewPath = "Assets/Scenes/Dev/UICommonPreview.unity";
    private static readonly Color Ink = Hex("3A2618"), Sub = Hex("7A5C3E"), Wood = Hex("B7793F"), Ring = Hex("DCC6A0");
    private static TMP_FontAsset Jua => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.Jua);
    private static TMP_FontAsset Medium => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.MediumDynamic);
    private static TMP_FontAsset Bold => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.Bold);
    private static Color Hex(string code) { Color color; ColorUtility.TryParseHtmlString("#"+code,out color); return color; }
    private static Sprite Sprite(string name)
    {
        var paths = AssetDatabase.FindAssets(name+" t:Texture2D",new[]{"Assets/Art"}).Select(AssetDatabase.GUIDToAssetPath)
            .Where(path=>Path.GetFileNameWithoutExtension(path)==name).ToArray();
        if(paths.Length!=1) throw new InvalidOperationException("스프라이트 경로 확인 필요: "+name);
        var sprites=AssetDatabase.LoadAllAssetsAtPath(paths[0]).OfType<Sprite>().ToArray();
        if(sprites.Length!=1) throw new InvalidOperationException("단일 스프라이트 필요: "+paths[0]);
        return sprites[0];
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));
        var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.sizeDelta=size;return rect;
    }
    private static void Stretch(RectTransform rect,float padding=0)
    {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*padding;rect.offsetMax=Vector2.one*-padding;}
    private static Image MakeImage(string name,Transform parent,Sprite sprite,Color color,bool sliced=true,bool raycast=false)
    {
        var rect=Rect(name,parent,Vector2.zero);Stretch(rect);
        var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.type=sliced?Image.Type.Sliced:Image.Type.Simple;
        image.color=color;image.raycastTarget=raycast;return image;
    }
    private static TMP_Text Text(string name,Transform parent,string value,TMP_FontAsset font,float size,Color color)
    {
        var rect=Rect(name,parent,new Vector2(240,48));var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font;text.fontSize=size;text.fontStyle=FontStyles.Normal;text.text=value;text.color=color;
        text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;return text;
    }
    private static void TopLeft(RectTransform rect,float x,float y,Vector2 size)
    {rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=size;}
    private static void CenterTop(RectTransform rect,float x,float y,Vector2 size)
    {rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0.5f,0.5f);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=size;}
    private static Button Button(Image face,string prefix)
    {
        var button=face.gameObject.AddComponent<Button>();button.targetGraphic=face;
        button.navigation=new Navigation{mode=Navigation.Mode.None};button.transition=Selectable.Transition.SpriteSwap;
        button.spriteState=new SpriteState{highlightedSprite=Sprite(prefix+"_hover"),pressedSprite=Sprite(prefix+"_pressed"),disabledSprite=Sprite(prefix+"_disabled")};return button;
    }
    private static GameObject Primary()
    {
        var root=Rect("PrimaryButton",null,new Vector2(480,76));
        var glow=MakeImage("Glow",root,Sprite("btn_throw_highlight"),new Color32(138,90,46,140));Stretch(glow.rectTransform,-44);
        glow.gameObject.AddComponent<GlowPulse>().Configure(glow);
        var surface=MakeImage("Button",root,Sprite("btn_throw_normal"),Color.white,true,true);
        var button=Button(surface,"btn_throw");
        var label=Text("Label",surface.transform,"확인",Jua,36,Color.white);label.rectTransform.sizeDelta=new Vector2(440,50);
        var sub=Text("Subtitle",surface.transform,"보조 설명",Medium,18,new Color32(255,240,225,255));sub.rectTransform.sizeDelta=new Vector2(440,28);sub.gameObject.SetActive(false);
        surface.gameObject.AddComponent<ButtonPresentation>().Configure(button,surface,null,label,sub,glow.gameObject,true);
        return root.gameObject;
    }
    private static GameObject Secondary()
    {
        var root=Rect("SecondaryButton",null,new Vector2(480,76));
        var face=MakeImage("Button",root,Sprite("btn_rethrow_normal"),Color.white,true,true);var button=Button(face,"btn_rethrow");
        var border=MakeImage("Border",face.transform,Sprite("btn_rethrow_border"),Wood);
        var label=Text("Label",face.transform,"취소",Jua,36,Ink);label.rectTransform.sizeDelta=new Vector2(440,50);
        face.gameObject.AddComponent<ButtonPresentation>().Configure(button,face,border,label,null,null,false);return root.gameObject;
    }
    private static GameObject Round()
    {
        var root=Rect("RoundIconButton",null,new Vector2(64,64));
        var shadow=MakeImage("Shadow",root,Sprite("panel_shadow"),Color.white);Stretch(shadow.rectTransform,-48);shadow.rectTransform.anchoredPosition=Vector2.down*8;
        var face=MakeImage("Button",root,Sprite("pill_fill"),Color.white,false,true);var button=face.gameObject.AddComponent<Button>();button.targetGraphic=face;button.navigation=new Navigation{mode=Navigation.Mode.None};
        button.colors=new ColorBlock{normalColor=Color.white,highlightedColor=new Color(1.05f,1.05f,1.05f),pressedColor=new Color(0.9f,0.9f,0.9f),selectedColor=Color.white,disabledColor=new Color(1,1,1,0.45f),colorMultiplier=1,fadeDuration=0.08f};
        MakeImage("Border",face.transform,Sprite("btn_round_border"),Ring,false);
        var icon=MakeImage("Icon",face.transform,Sprite("icon_settings"),Sub,false);icon.rectTransform.anchorMin=icon.rectTransform.anchorMax=Vector2.one*0.5f;icon.rectTransform.sizeDelta=new Vector2(34,34);icon.preserveAspect=true;return root.gameObject;
    }
    private static GameObject Paper(Vector2 size)
    {
        var root=Rect("PaperCard",null,size);
        var shadow=MakeImage("Shadow",root,Sprite("panel_shadow"),Color.white);Stretch(shadow.rectTransform,-48);shadow.rectTransform.anchoredPosition=Vector2.down*10;
        var glow=MakeImage("Glow",root,Sprite("panel_glow"),new Color32(183,121,63,97));Stretch(glow.rectTransform,-48);
        MakeImage("Paper",root,Sprite("panel_paper"),Color.white);
        var border=MakeImage("EmphasisBorder",root,Sprite("turn_banner_border"),Wood);
        root.gameObject.AddComponent<PaperCardPresentation>().Configure(glow.gameObject,border.gameObject);return root.gameObject;
    }
    private static GameObject Gold()
    {
        var root=Paper(new Vector2(250,80));root.name="GoldDisplay";
        var coin=MakeImage("Coin",root.transform,Sprite("icon_coin"),Color.white,false);CenterTop(coin.rectTransform,46,40,new Vector2(56,56));coin.preserveAspect=true;
        var amount=Text("Amount",root.transform,"1,000",Jua,34,Ink);TopLeft(amount.rectTransform,88,21,new Vector2(146,38));amount.alignment=TextAlignmentOptions.MidlineLeft;return root;
    }
    private static GameObject Input()
    {
        var root=Rect("InputField",null,new Vector2(528,76));
        var well=root.gameObject.AddComponent<Image>();well.sprite=Sprite("slot_well");well.type=Image.Type.Sliced;
        var border=MakeImage("FocusBorder",root,Sprite("btn_rethrow_border"),Wood);
        var viewport=Rect("TextArea",root,Vector2.zero);Stretch(viewport,0);viewport.offsetMin=new Vector2(24,0);viewport.offsetMax=new Vector2(-24,0);viewport.gameObject.AddComponent<RectMask2D>();
        var value=Text("Text",viewport,"",Medium,26,Ink);Stretch(value.rectTransform);value.alignment=TextAlignmentOptions.MidlineLeft;
        var placeholder=Text("Placeholder",viewport,"입력해 주세요",Medium,24,Hex("A88E6E"));Stretch(placeholder.rectTransform);placeholder.alignment=TextAlignmentOptions.MidlineLeft;
        var input=root.gameObject.AddComponent<TMP_InputField>();input.targetGraphic=well;input.textViewport=viewport;input.textComponent=value;input.placeholder=placeholder;input.lineType=TMP_InputField.LineType.SingleLine;
        root.gameObject.AddComponent<InputFocusPresentation>().Configure(border);return root.gameObject;
    }
    private static GameObject Segmented()
    {
        var root=Rect("SegmentedControl",null,new Vector2(528,80));MakeImage("Well",root,Sprite("slot_well"),Color.white);
        var faces=new Image[2];var labels=new TMP_Text[2];var buttons=new Button[2];
        for(int i=0;i<2;i++)
        {
            var face=MakeImage(i==0?"First":"Second",root,Sprite("btn_throw_normal"),Color.white);
            face.rectTransform.anchorMin=new Vector2(i*0.5f,0);face.rectTransform.anchorMax=new Vector2((i+1)*0.5f,1);
            face.rectTransform.offsetMin=new Vector2(6,6);face.rectTransform.offsetMax=new Vector2(-6,-6);
            // 간격 12를 제외한 두 칸 폭이 (W-12)/2가 되도록 가운데 여백은 0으로 둔다.
            if(i==0)face.rectTransform.offsetMax=new Vector2(0,-6);else face.rectTransform.offsetMin=new Vector2(0,6);
            face.raycastTarget=true;
            buttons[i]=face.gameObject.AddComponent<Button>();buttons[i].targetGraphic=face;buttons[i].transition=Selectable.Transition.None;
            labels[i]=Text("Label",face.transform,i==0?"공개":"비공개",Jua,28,Sub);Stretch(labels[i].rectTransform);
            faces[i]=face;
        }
        root.gameObject.AddComponent<SegmentedPresentation>().Configure(faces,labels,buttons);return root.gameObject;
    }
    private static GameObject Nested(string name,Transform parent)
    {return (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab"),parent);}
    private static GameObject Popup()
    {
        var root=Rect("PopupFrame",null,new Vector2(1920,1080));var group=root.gameObject.AddComponent<CanvasGroup>();
        var dim=MakeImage("Dim",root,null,new Color(58/255f,38/255f,24/255f,0.59f),false,true);
        var paper=Nested("PaperCard",root);paper.name="Popup";var body=paper.GetComponent<RectTransform>();body.sizeDelta=new Vector2(1040,720);
        paper.transform.Find("Paper").GetComponent<Image>().raycastTarget=true;
        var title=Text("Title",body,"팝업 제목",Jua,48,Ink);TopLeft(title.rectTransform,56,34,new Vector2(520,60));title.alignment=TextAlignmentOptions.MidlineLeft;
        var sub=Text("Description",body,"팝업 설명 문구가 들어갑니다.",Medium,22,Sub);TopLeft(sub.rectTransform,56,94,new Vector2(850,36));sub.alignment=TextAlignmentOptions.MidlineLeft;
        var close=Nested("RoundIconButton",body);close.name="CloseButton";var closeRect=close.GetComponent<RectTransform>();closeRect.anchorMin=closeRect.anchorMax=new Vector2(1,1);closeRect.anchoredPosition=new Vector2(-56,-58);
        close.transform.Find("Button/Icon").GetComponent<Image>().sprite=Sprite("icon_close");
        var gold=Nested("GoldDisplay",body);gold.name="Gold";var goldRect=gold.GetComponent<RectTransform>();goldRect.anchorMin=goldRect.anchorMax=new Vector2(1,1);goldRect.pivot=new Vector2(0,1);goldRect.anchoredPosition=new Vector2(-366,-20);gold.SetActive(false);
        root.gameObject.AddComponent<PopupPresentation>().Configure(body,group,close.GetComponentInChildren<Button>());return root.gameObject;
    }
    [MenuItem("Tools/Yutnori/UIv2/Generate Common Prefabs and Preview")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Assets/Scenes/Dev");AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            foreach(var pair in new Dictionary<string,Func<GameObject>>{{"PrimaryButton",Primary},{"SecondaryButton",Secondary},{"RoundIconButton",Round},{"PaperCard",()=>Paper(new Vector2(480,180))},{"GoldDisplay",Gold},{"InputField",Input},{"SegmentedControl",Segmented},{"PopupFrame",Popup}})
            {
                var root=pair.Value();SceneManager.MoveGameObjectToScene(root,scene);PrefabUtility.SaveAsPrefabAsset(root,Folder+pair.Key+".prefab");UnityEngine.Object.DestroyImmediate(root);
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        GeneratePreview();Debug.Log("[UIv2] 공통 프리팹 8종과 확인용 씬 생성 완료. 기존 UI/빌드 목록 변경 없음.");
    }
    private static void Place(GameObject obj,Transform parent,float x,float y,Vector2? size=null)
    {obj.transform.SetParent(parent,false);var rect=obj.GetComponent<RectTransform>();TopLeft(rect,x,y,size??rect.sizeDelta);}
    private static void Caption(Transform parent,string value,float x,float y,float width=680)
    {var text=Text("Caption",parent,value,Medium,20,Sub);TopLeft(text.rectTransform,x,y,new Vector2(width,32));text.alignment=TextAlignmentOptions.MidlineLeft;}
    private static void GeneratePreview()
    {
        var original=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var cameraObject=new GameObject("Main Camera");var camera=cameraObject.AddComponent<Camera>();cameraObject.tag="MainCamera";camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=540;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Hex("F3E6CF");
            new GameObject("Directional Light").AddComponent<Light>().type=LightType.Directional;
            var canvasObject=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
            var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;scaler.referencePixelsPerUnit=100;
            var eventObject=new GameObject("EventSystem",typeof(EventSystem));eventObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var bg=MakeImage("Background",canvas.transform,Sprite("bg_hanji_tile"),Color.white);bg.type=Image.Type.Tiled;
            var heading=Text("Heading",canvas.transform,"UIv2 공통 부품 확인",Jua,48,Ink);TopLeft(heading.rectTransform,70,30,new Vector2(1100,70));heading.alignment=TextAlignmentOptions.MidlineLeft;
            Caption(canvas.transform,"명세 0-3 · 0-4 / 1920×1080 / 표시 전용",72,102);
            Caption(canvas.transform,"PrimaryButton · 기본 / 비활성",80,145);
            var primary=Nested("PrimaryButton",canvas.transform);Place(primary,canvas.transform,80,184,new Vector2(240,76));primary.transform.Find("Button/Label").GetComponent<TMP_Text>().text="확인";
            var disabled=Nested("PrimaryButton",canvas.transform);Place(disabled,canvas.transform,350,184,new Vector2(240,76));disabled.GetComponentInChildren<Button>().interactable=false;disabled.GetComponentInChildren<ButtonPresentation>().Refresh();disabled.transform.Find("Button/Label").GetComponent<TMP_Text>().text="비활성";
            Caption(canvas.transform,"PrimaryButton · 발광 + 보조 글자",80,290);
            var bonus=Nested("PrimaryButton",canvas.transform);Place(bonus,canvas.transform,80,330,new Vector2(510,104));var bv=bonus.GetComponentInChildren<ButtonPresentation>();bv.SetSubtitle("열려 있는 공개방에 바로 들어가요");bv.SetGlowVisible(true);bonus.transform.Find("Button/Label").GetComponent<TMP_Text>().text="빠른 참가";
            Caption(canvas.transform,"SecondaryButton · 보조 버튼",80,464);
            var secondary=Nested("SecondaryButton",canvas.transform);Place(secondary,canvas.transform,80,504,new Vector2(510,76));
            Caption(canvas.transform,"RoundIconButton 64 / GoldDisplay 250×80",80,609);
            var round=Nested("RoundIconButton",canvas.transform);Place(round,canvas.transform,100,654);
            var gold=Nested("GoldDisplay",canvas.transform);Place(gold,canvas.transform,260,644);
            Caption(canvas.transform,"InputField · 선택하면 포커스 테두리 표시",80,749);
            var input=Nested("InputField",canvas.transform);Place(input,canvas.transform,80,788);
            Caption(canvas.transform,"SegmentedControl · 클릭하면 선택 표시 변경",80,892);
            var segment=Nested("SegmentedControl",canvas.transform);Place(segment,canvas.transform,80,931);
            Caption(canvas.transform,"PopupFrame + PaperCard · 오른쪽은 조립 확인을 위해 Dim만 숨김",830,145,1050);
            var popup=Nested("PopupFrame",canvas.transform);Stretch(popup.GetComponent<RectTransform>());popup.transform.Find("Dim").gameObject.SetActive(false);
            popup.transform.Find("Popup").GetComponent<RectTransform>().anchoredPosition=new Vector2(410,-10);
            popup.transform.Find("Popup/Title").GetComponent<TMP_Text>().text="팝업 공통 틀";
            popup.transform.Find("Popup/Gold").gameObject.SetActive(true);
            var paper=Nested("PaperCard",popup.transform.Find("Popup"));Place(paper,popup.transform.Find("Popup"),70,200,new Vector2(900,270));paper.GetComponent<PaperCardPresentation>().SetEmphasis(true,true);
            var body=Text("Body",paper.transform,"PaperCard\n그림자 · 발광 · 종이 · 강조 테두리",Medium,24,Sub);body.rectTransform.sizeDelta=new Vector2(800,140);body.textWrappingMode=TextWrappingModes.Normal;
            Caption(popup.transform.Find("Popup"),"PopupFrame 기본 크기 1040×720 / 크기 변경 가능",70,530,900);
            var confirm=Nested("PrimaryButton",popup.transform.Find("Popup"));Place(confirm,popup.transform.Find("Popup"),280,590,new Vector2(480,76));
            // 모달 안 버튼을 눌러도 예제에서는 네트워크/게임 로직을 호출하지 않는다.
            EditorSceneManager.SaveScene(scene,PreviewPath);
        }
        finally{SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(scene,true);}
    }
    [MenuItem("Tools/Yutnori/UIv2/Capture Common Preview FHD")]
    public static void Capture()
    {
        var original=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(PreviewPath,OpenSceneMode.Additive);
        RenderTexture target=null;Texture2D image=null;var previous=RenderTexture.active;
        try
        {
            var camera=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Camera>()).First();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            target=new RenderTexture(1920,1080,24);target.Create();camera.targetTexture=target;
            foreach(var rect in scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Canvas>()))rect.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
            Canvas.ForceUpdateCanvases();foreach(var text in scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<TMP_Text>(true)))text.ForceMeshUpdate(true);
            camera.Render();RenderTexture.active=target;image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
            Directory.CreateDirectory("Docs/UI/Reports");File.WriteAllBytes("Docs/UI/Reports/ui_common_preview_fhd.png",image.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;if(image!=null)UnityEngine.Object.DestroyImmediate(image);if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(scene,true);}
    }
    [MenuItem("Tools/Yutnori/UIv2/Validate Common Prefabs")]
    public static string Validate()
    {
        var results = new List<string>();
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (var name in new[] { "PrimaryButton", "SecondaryButton", "RoundIconButton", "GoldDisplay", "InputField", "SegmentedControl", "PopupFrame", "PaperCard" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
                Require(asset != null, name + " asset");
                var root = UnityEngine.Object.Instantiate(asset);
                SceneManager.MoveGameObjectToScene(root, scene);
                try
                {
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) == 0, name + " missing script");
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                        Require(text.font == Jua || text.font == Medium || text.font == Bold, name + " font");
                    if (name == "PrimaryButton" || name == "SecondaryButton")
                    {
                        var button = root.GetComponentInChildren<Button>();
                        var view = button.GetComponent<ButtonPresentation>();
                        Require(button.transition == Selectable.Transition.SpriteSwap, name + " swap");
                        Require(button.spriteState.highlightedSprite != null && button.spriteState.pressedSprite != null && button.spriteState.disabledSprite != null, name + " states");
                        button.interactable = false; view.Refresh();
                        Require(((Image)button.targetGraphic).color == Color.white, name + " disabled face");
                        Require(button.transform.Find("Label").GetComponent<TMP_Text>().color == (Color)new Color32(168,154,132,255), name + " disabled label");
                        button.interactable = true; view.Refresh();
                        if (name == "PrimaryButton")
                        {
                            Require(((Image)button.targetGraphic).color == Hex("915F30"), "Primary enabled tint");
                            view.SetSubtitle("테스트"); view.SetGlowVisible(true);
                            Require(root.transform.Find("Glow").gameObject.activeSelf && button.transform.Find("Subtitle").gameObject.activeSelf, "Primary optional display");
                            view.SetSubtitle(""); view.SetGlowVisible(false);
                            Require(!root.transform.Find("Glow").gameObject.activeSelf && !button.transform.Find("Subtitle").gameObject.activeSelf, "Primary optional hidden");
                        }
                    }
                    if (name == "RoundIconButton")
                    {
                        Require(root.GetComponent<RectTransform>().sizeDelta == new Vector2(64,64), "Round size");
                        Require(root.transform.Find("Button/Icon").GetComponent<RectTransform>().sizeDelta == new Vector2(34,34), "Round icon size");
                    }
                    if (name == "GoldDisplay") Require(root.GetComponent<RectTransform>().sizeDelta == new Vector2(250,80), "Gold size");
                    if (name == "InputField")
                    {
                        var focus = root.GetComponent<InputFocusPresentation>();
                        focus.SetFocused(true); Require(root.transform.Find("FocusBorder").GetComponent<Image>().enabled, "Input focus");
                        focus.SetFocused(false); Require(!root.transform.Find("FocusBorder").GetComponent<Image>().enabled, "Input blur");
                        Require(root.GetComponent<TMP_InputField>().textComponent.font == Medium, "Input Dynamic body");
                    }
                    if (name == "SegmentedControl")
                    {
                        var view = root.GetComponent<SegmentedPresentation>();
                        root.SetActive(false); root.SetActive(true);
                        root.transform.Find("Second").GetComponent<Button>().onClick.Invoke();
                        Require(view.SelectedIndex == 1 && root.transform.Find("Second").GetComponent<Image>().color == Hex("915F30"), "Segment second click");
                        root.transform.Find("First").GetComponent<Button>().onClick.Invoke();
                        Require(view.SelectedIndex == 0 && root.transform.Find("First").GetComponent<Image>().raycastTarget, "Segment first click");
                    }
                    if (name == "PaperCard")
                    {
                        var view = root.GetComponent<PaperCardPresentation>(); view.SetSize(new Vector2(700,300)); view.SetEmphasis(true,true);
                        Require(root.GetComponent<RectTransform>().sizeDelta == new Vector2(700,300), "Paper resize");
                        Require(root.transform.Find("Glow").gameObject.activeSelf && root.transform.Find("EmphasisBorder").gameObject.activeSelf, "Paper emphasis");
                    }
                    if (name == "PopupFrame")
                    {
                        var dim = root.transform.Find("Dim").GetComponent<Image>();
                        Require(dim.raycastTarget && Mathf.Approximately(dim.color.a,0.59f), "Popup dim");
                        Require(root.transform.Find("Popup").GetComponent<RectTransform>().sizeDelta == new Vector2(1040,720), "Popup size");
                        root.GetComponent<PopupPresentation>().SetSize(new Vector2(900,600));
                        Require(root.transform.Find("Popup").GetComponent<RectTransform>().sizeDelta == new Vector2(900,600), "Popup resize");
                        Require(root.transform.Find("Popup/CloseButton/Button/Icon").GetComponent<Image>().sprite.name == "icon_close", "Popup close");
                    }
                    results.Add(name + ": PASS");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            Require(!EditorBuildSettings.scenes.Any(item => item.path == PreviewPath), "Preview excluded from build");
            results.Add("Preview build exclusion: PASS");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        string report = string.Join("\n", results);
        Directory.CreateDirectory("Docs/UI/Reports");
        File.WriteAllText("Docs/UI/Reports/stage2_common_validation.txt", report + "\n");
        Debug.Log(report); return report;
    }
    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("[UIv2 validation] " + name);
    }

}
