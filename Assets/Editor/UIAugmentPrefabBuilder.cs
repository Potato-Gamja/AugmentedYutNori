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

/// <summary>명세 5장 UIv2 증강 선택 화면을 생성한다. 미리보기 후보는 Editor 전용이다.</summary>
public static class UIAugmentPrefabBuilder
{
    public const string PanelPath="Assets/Prefabs/UIv2/Panels/AugmentSelectPanel.prefab";
    public const string CardPath="Assets/Prefabs/UIv2/Items/AugmentCardView.prefab";
    private const string RootPath="Assets/Prefabs/UI/UIRoot.prefab";
    private static TMP_FontAsset Jua=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.Jua);
    private static TMP_FontAsset Medium=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.MediumDynamic);
    private static TMP_FontAsset Bold=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.Bold);
    private static Color Hex(string code){Color color;ColorUtility.TryParseHtmlString("#"+code,out color);return color;}
    private static RectTransform Rect(string name,Transform parent,Vector2 size)
    {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.sizeDelta=size;return rect;}
    private static void Stretch(RectTransform rect,float inset=0)
    {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*inset;rect.offsetMax=Vector2.one*-inset;}
    private static void Top(RectTransform rect,float x,float y,float width,float height)
    {rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
    private static void Center(RectTransform rect,float x,float y,float width,float height)
    {Top(rect,x,y,width,height);rect.pivot=Vector2.one*0.5f;}
    private static Sprite Sprite(string name)
    {var path=AssetDatabase.FindAssets(name+" t:Texture2D",new[]{"Assets/Art"}).Select(AssetDatabase.GUIDToAssetPath).Single(item=>Path.GetFileNameWithoutExtension(item)==name);return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();}
    private static Image Image(string name,Transform parent,Sprite sprite,Color color,bool sliced=true,bool raycast=false)
    {var rect=Rect(name,parent,Vector2.zero);Stretch(rect);var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color;image.type=sliced?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.raycastTarget=raycast;return image;}
    private static TMP_Text Text(string name,Transform parent,string value,TMP_FontAsset font,float size,string color)
    {var rect=Rect(name,parent,new Vector2(300,60));var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=size;text.text=value;text.color=Hex(color);text.raycastTarget=false;text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;return text;}
    private static GameObject Common(string name,Transform parent)
    {return (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UICommonPrefabBuilder.Folder+name+".prefab"),parent);}
    private static void Bind(UnityEngine.Object owner,string name,UnityEngine.Object value)
    {var so=new SerializedObject(owner);var property=so.FindProperty(name);if(property==null)throw new InvalidOperationException("필드 없음: "+name);property.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    private static void BindArray(UnityEngine.Object owner,string name,IEnumerable<UnityEngine.Object> values)
    {var so=new SerializedObject(owner);var property=so.FindProperty(name);var list=values.ToArray();property.arraySize=list.Length;for(int i=0;i<list.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=list[i];so.ApplyModifiedPropertiesWithoutUndo();}
    public static T Field<T>(UnityEngine.Object owner,string name) where T:UnityEngine.Object
    {return (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;}
    private static GameObject Card()
    {
        var root=Rect("AugmentCardView",null,new Vector2(340,440));var view=root.gameObject.AddComponent<AugmentCardView>();
        var layout=root.gameObject.AddComponent<LayoutElement>();layout.minWidth=layout.preferredWidth=340;layout.minHeight=layout.preferredHeight=440;layout.flexibleWidth=layout.flexibleHeight=0;
        var visual=Common("PaperCard",root);visual.name="Visual";Stretch(visual.GetComponent<RectTransform>());
        var paper=visual.transform.Find("Paper").GetComponent<Image>();paper.raycastTarget=true;
        var button=root.gameObject.AddComponent<Button>();button.targetGraphic=paper;button.transition=Selectable.Transition.None;button.navigation=new Navigation{mode=Navigation.Mode.None};
        var glow=visual.transform.Find("Glow").GetComponent<Image>();glow.gameObject.SetActive(false);
        var selected=visual.transform.Find("EmphasisBorder").GetComponent<Image>();selected.gameObject.SetActive(false);
        var hover=Image("HoverBorder",visual.transform,Sprite("btn_rethrow_border"),Hex("DCC6A0"));Stretch(hover.rectTransform,8);hover.gameObject.SetActive(false);
        var icon=Image("Icon",visual.transform,Sprite("aug_assassin"),Color.white,false);Center(icon.rectTransform,170,112,160,160);icon.preserveAspect=true;
        var name=Text("Name",visual.transform,"암살자",Jua,42,"3A2618");Center(name.rectTransform,170,232,300,60);
        var category=Image("CategoryPill",visual.transform,Sprite("pill_fill"),Hex("94584E"));Center(category.rectTransform,170,282,82,34);
        var categoryText=Text("Label",category.transform,"공격",Bold,20,"FFFFFF");Stretch(categoryText.rectTransform);
        var description=Text("Description",visual.transform,"",Medium,22,"7A5C3E");Top(description.rectTransform,35,320,270,100);description.alignment=TextAlignmentOptions.Top;description.textWrappingMode=TextWrappingModes.Normal;description.enableAutoSizing=true;description.fontSizeMax=22;description.fontSizeMin=18;description.maxVisibleLines=3;description.overflowMode=TextOverflowModes.Ellipsis;
        var badge=Image("CheckBadge",visual.transform,Sprite("badge_check_red"),Color.white,false);Center(badge.rectTransform,304,36,40,40);
        var check=Image("Check",badge.transform,Sprite("icon_check"),Color.white,false);Center(check.rectTransform,20,20,20,20);badge.gameObject.SetActive(false);
        Bind(view,"augmentIcon",icon);Bind(view,"augumentName",name);Bind(view,"augmentDescription",description);
        string[] keys={"assassin","hunter","marathoner","monk","sprinter","loner","pacifist","friend_shield","gambler","rethrow","contrarian"};BindArray(view,"augmentIcons",keys.Select(key=>Sprite("aug_"+key)).Concat(new[]{VeteranUIIntegration.Icon} ));
        Bind(view,"visual",visual.GetComponent<RectTransform>());Bind(view,"categoryPill",category);Bind(view,"categoryLabel",categoryText);Bind(view,"hoverBorder",hover);Bind(view,"selectedBorder",selected);Bind(view,"selectedGlow",glow);Bind(view,"checkBadge",badge);Bind(view,"checkMark",check);
        BindArray(view,"teamCheckBadges",new[]{"red","blue","green","yellow"}.Select(key=>Sprite("badge_check_"+key)));return root.gameObject;
    }
    private static GameObject Panel()
    {
        var root=Rect("AugmentSelectPanel",null,new Vector2(1920,1080));Stretch(root);var panel=root.gameObject.AddComponent<AugmentSelectPanel>();
        var layer=new SerializedObject(panel);layer.FindProperty("layer").enumValueIndex=(int)UILayer.Popup;layer.ApplyModifiedPropertiesWithoutUndo();
        var dim=Image("Dim",root,null,new Color(58/255f,38/255f,24/255f,0.59f),false,true);dim.rectTransform.offsetMax=new Vector2(0,-130);
        var modal=Common("PaperCard",root);modal.name="Modal";modal.GetComponent<RectTransform>().sizeDelta=new Vector2(1200,700);modal.transform.Find("Paper").GetComponent<Image>().raycastTarget=true;
        var border=modal.transform.Find("EmphasisBorder").GetComponent<Image>();border.gameObject.SetActive(true);Bind(panel,"modalBorder",border);
        var title=Text("Title",modal.transform,"빨강 팀, 직업을 고르세요",Jua,46,"922A26");Center(title.rectTransform,510,66,900,64);title.alignment=TextAlignmentOptions.MidlineLeft;Bind(panel,"titleText",title);
        var sub=Text("Description",modal.transform,"카드를 누르면 바로 정해져요. 고른 증강은 이번 판 내내 적용돼요.",Medium,22,"7A5C3E");Center(sub.rectTransform,560,114,1000,40);sub.alignment=TextAlignmentOptions.MidlineLeft;
        var cards=Rect("Cards",modal.transform,new Vector2(1060,440));Top(cards,70,180,1060,440);var layout=cards.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=20;layout.childAlignment=TextAnchor.MiddleCenter;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=false;
        var footer=Text("Footer",modal.transform,"남은 시간은 위쪽 타이머에서 확인하세요",Medium,19,"A88E6E");Center(footer.rectTransform,600,666,1050,32);
        Bind(panel,"augumentCardViewPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(CardPath));Bind(panel,"augmentCardContainer",cards);return root.gameObject;
    }
    [MenuItem("Tools/Yutnori/UIv2/Generate Augment Prefabs")]
    public static void Generate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play를 종료해 주세요.");var scene=EditorSceneManager.NewPreviewScene();
        try{var card=Card();SceneManager.MoveGameObjectToScene(card,scene);PrefabUtility.SaveAsPrefabAsset(card,CardPath);UnityEngine.Object.DestroyImmediate(card);var panel=Panel();SceneManager.MoveGameObjectToScene(panel,scene);PrefabUtility.SaveAsPrefabAsset(panel,PanelPath);UnityEngine.Object.DestroyImmediate(panel);}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    private static void Replace(UIManager manager,string file,List<string> rows)
    {
        var so=new SerializedObject(manager);var list=so.FindProperty("prefabs");int count=0;
        for(int i=0;i<list.arraySize;i++){var item=list.GetArrayElementAtIndex(i);if(!(item.objectReferenceValue is AugmentSelectPanel))continue;rows.Add("| "+file+" | "+manager.name+" · prefabs["+i+"] | "+AssetDatabase.GetAssetPath(item.objectReferenceValue)+" | "+PanelPath+" |");item.objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath).GetComponent<AugmentSelectPanel>();count++;}
        if(count!=1)throw new InvalidOperationException("증강 패널 등록 수: "+count);so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
    }
    [MenuItem("Tools/Yutnori/UIv2/Replace Augment References")]
    public static void ReplaceReferences()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play를 종료해 주세요.");
        var original=SceneManager.GetActiveScene();var rows=new List<string>();var root=PrefabUtility.LoadPrefabContents(RootPath);
        try{Replace(root.GetComponentInChildren<UIManager>(true),RootPath,rows);PrefabUtility.SaveAsPrefabAsset(root,RootPath);}finally{PrefabUtility.UnloadPrefabContents(root);}
        var game=SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");bool opened=!game.isLoaded;if(opened)game=EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Additive);
        try{Replace(game.GetRootGameObjects().SelectMany(item=>item.GetComponentsInChildren<UIManager>(true)).Single(),game.path,rows);EditorSceneManager.SaveScene(game);}
        finally{if(opened)EditorSceneManager.CloseScene(game,true);SceneManager.SetActiveScene(original);}
        Directory.CreateDirectory(UIScreenPreparation.Reports);File.WriteAllText(UIScreenPreparation.Reports+"stage6_reference_changes.md","# 6단계 참조 교체\n\n| 파일 | 오브젝트·필드 | 이전 | 이후 |\n|---|---|---|---|\n"+string.Join("\n",rows)+"\n\n기존 프리팹은 보존했습니다. 같은 타입은 한 번만 등록합니다.\n");
    }
    public static List<AugmentDefinition> Choices()
    {var catalog=new AugmentCatalog(JsonUtility.FromJson<AugmentDefinitionFile>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/augments.json").text),AugmentFactories.Create());return new[]{"Assassin","Hunter","Gambler"}.Select(catalog.Find).ToList();}
    public sealed class Preview : IDisposable
    {
        public AugmentSelectPanel Panel;public RenderTexture Target;private Scene scene;private Camera camera;
        public Preview(int team,bool picked=false,bool online=false)
        {
            scene=EditorSceneManager.NewPreviewScene();var cameraRoot=new GameObject("FHD Camera");SceneManager.MoveGameObjectToScene(cameraRoot,scene);camera=cameraRoot.AddComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=540;
            var light=new GameObject("Directional Light");SceneManager.MoveGameObjectToScene(light,scene);light.AddComponent<Light>().type=LightType.Directional;
            var root=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(root,scene);var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;root.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
            var background=Image("Background",root.transform,Sprite("bg_hanji_tile"),Color.white);background.type=UnityEngine.UI.Image.Type.Tiled;
            // 서버 없는 캡처에서 Dim 상단 130이 비어 있는지 확인하는 표시용 배너/시간 샘플.
            var banner=Common("PaperCard",root.transform);banner.name="BannerSample";Top(banner.GetComponent<RectTransform>(),412,20,1040,100);
            var turn=Text("Title",banner.transform,new[]{"빨강","파랑","초록","노랑"}[team]+" 팀 증강 선택",Jua,30,"3A2618");Center(turn.rectTransform,480,40,850,44);
            var timer=Text("TimerSample",banner.transform,"29",Jua,36,"3A2618");Center(timer.rectTransform,990,50,70,56);
            Panel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath),root.transform).GetComponent<AugmentSelectPanel>();Panel.Setup(Choices(),id=>{},team,online);Panel.Open();
            if(picked){var cards=Panel.GetComponentsInChildren<AugmentCardView>();foreach(var card in cards)card.SetInput(false);cards[1].SetSelected(true);Field<RectTransform>(cards[1],"visual").anchoredPosition=Vector2.up*8;}
            Target=new RenderTexture(1920,1080,24);Target.Create();camera.targetTexture=Target;
        }
        public void Render(){Canvas.ForceUpdateCanvases();foreach(var text in Panel.transform.parent.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate(true);Canvas.ForceUpdateCanvases();camera.Render();}
        public void Save(string file){Render();var previous=RenderTexture.active;Texture2D image=null;try{RenderTexture.active=Target;image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();Directory.CreateDirectory(UIScreenPreparation.Reports);File.WriteAllBytes(UIScreenPreparation.Reports+file,image.EncodeToPNG());}finally{RenderTexture.active=previous;if(image!=null)UnityEngine.Object.DestroyImmediate(image);}}
        public void Dispose(){camera.targetTexture=null;Target.Release();UnityEngine.Object.DestroyImmediate(Target);EditorSceneManager.ClosePreviewScene(scene);}
    }
    [MenuItem("Tools/Yutnori/UIv2/Capture Augment States FHD")]
    public static void Capture()
    {for(int team=0;team<4;team++){using(var preview=new Preview(team))preview.Save("stage6_augment_"+new[]{"red","blue","green","yellow"}[team]+"_fhd.png");}using(var preview=new Preview(0,true))preview.Save("stage6_augment_picked_fhd.png");}
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException("[Augment UI] "+message);}
    [MenuItem("Tools/Yutnori/UIv2/Validate Augment UI")]
    public static string Validate()
    {
        var results=new List<string>();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(CardPath).GetComponent<AugmentCardView>();var so=new SerializedObject(prefab);
        var ids=(string[])typeof(AugmentCardView).GetField("AugmentIds",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);
        var icons=so.FindProperty("augmentIcons");Require(icons.arraySize==12,"아이콘 12종");string[] keys={"assassin","hunter","marathoner","monk","sprinter","loner","pacifist","friend_shield","gambler","rethrow","contrarian"};for(int i=0;i<11;i++)Require(icons.GetArrayElementAtIndex(i).objectReferenceValue.name=="aug_"+keys[i],"아이콘 순서 "+ids[i]);
        var catalog=new AugmentCatalog(JsonUtility.FromJson<AugmentDefinitionFile>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/augments.json").text),AugmentFactories.Create());
        using(var preview=new Preview(0))
        {
            Require(preview.Panel.transform.Find("Dim").GetComponent<RectTransform>().offsetMax.y==-130,"Dim 위쪽 130");
            var card=preview.Panel.GetComponentsInChildren<AugmentCardView>()[0];string[] categories={"공격","공격","이동","이동","이동","방어","방어","방어","운","운","운","이동"};
            foreach(int i in Enumerable.Range(0,12)){card.Init(catalog.Find(ids[i]),id=>{});preview.Render();var description=Field<TMP_Text>(card,"augmentDescription");Require(Field<TMP_Text>(card,"categoryLabel").text==categories[i],"분류 "+ids[i]);Require(description.textInfo.lineCount<=3&&!description.isTextTruncated,"설명 3줄 이내 "+ids[i]);}
            card.SetHovered(true);Require(Field<Image>(card,"hoverBorder").gameObject.activeSelf,"오버 테두리");card.SetSelected(true);card.SetInput(false);Require(Field<Image>(card,"selectedGlow").gameObject.activeSelf&&Field<Image>(card,"checkBadge").gameObject.activeSelf&&!card.GetComponent<Button>().interactable,"누름 스타일");
        }
        Require(icons.GetArrayElementAtIndex(11).objectReferenceValue==VeteranUIIntegration.Icon,"노장 임시 아이콘");
        results.Add("아이콘 12종 순서 · 분류 표 · 설명 3줄/잘림 없음 · Dim 상단 130 · 오버/선택 표시: PASS");
        for(int team=0;team<4;team++){using(var preview=new Preview(team,true)){preview.Render();Require(Field<TMP_Text>(preview.Panel,"titleText").color==AugmentCardView.TeamColor(team,true),"제목 팀 색");var card=preview.Panel.GetComponentsInChildren<AugmentCardView>()[1];Require(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(Field<Image>(card,"checkBadge").sprite))=="badge_check_"+new[]{"red","blue","green","yellow"}[team],"팀별 체크");Require(Field<Image>(card,"checkMark").color==(team==3?(Color)new Color32(58,38,24,255):Color.white),"체크 글자색");}}
        using(var preview=new Preview(2,false,true))Require(Field<TMP_Text>(preview.Panel,"titleText").text=="직업을 고르세요","온라인 제목");results.Add("4팀 제목·테두리·체크 · 노랑 Ink 체크 · 온라인 제목: PASS");
        string report=string.Join("\n",results)+"\n";Directory.CreateDirectory(UIScreenPreparation.Reports);File.WriteAllText(UIScreenPreparation.Reports+"stage6_editor_validation.txt",report);return report;
    }
}

public sealed class UIAugmentPreviewWindow : EditorWindow
{
    private UIAugmentPrefabBuilder.Preview preview;private int team;private bool picked,online;
    [MenuItem("Tools/Yutnori/UIv2/Augment State Preview")]
    public static void ShowWindow(){GetWindow<UIAugmentPreviewWindow>("Augment UI Preview").Show();}
    private void OnEnable(){if(AssetDatabase.LoadAssetAtPath<GameObject>(UIAugmentPrefabBuilder.PanelPath)!=null)preview=new UIAugmentPrefabBuilder.Preview(team,picked,online);}
    private void OnDisable(){if(preview!=null){preview.Dispose();preview=null;}}
    private void OnGUI()
    {
        if(preview==null){EditorGUILayout.HelpBox("Generate Augment Prefabs를 먼저 실행해 주세요.",UnityEditor.MessageType.Info);return;}
        EditorGUI.BeginChangeCheck();team=EditorGUILayout.Popup("팀",team,new[]{"빨강","파랑","초록","노랑"});picked=EditorGUILayout.Toggle("누름 상태",picked);online=EditorGUILayout.Toggle("온라인 제목",online);if(EditorGUI.EndChangeCheck()){preview.Dispose();preview=new UIAugmentPrefabBuilder.Preview(team,picked,online);}
        preview.Render();var rect=GUILayoutUtility.GetRect(1920,1080,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));GUI.DrawTexture(rect,preview.Target,ScaleMode.ScaleToFit,false);
    }
}
