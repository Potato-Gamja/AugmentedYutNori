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

/// <summary>명세 6장 팝업 7종과 반복 항목을 재생성한다. 기존 요청/저장 코드는 사용한다.</summary>
public static class UIPopupPrefabBuilder
{
    public const string Folder="Assets/Prefabs/UIv2/Popups/", Items="Assets/Prefabs/UIv2/Items/";
    public static readonly string[] Names={"GachaPanel","GachaResultView","CollectionPanel","ProfilePanel","AchievementPanel","SettingsPanel","ReconnectPanel"};
    private static TMP_FontAsset Jua=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.Jua);
    private static TMP_FontAsset Medium=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.MediumDynamic);
    private static TMP_FontAsset Bold=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UIScreenPreparation.Bold);
    public static Color Hex(string value){Color color;ColorUtility.TryParseHtmlString("#"+value,out color);return color;}
    private static RectTransform Rect(string name,Transform parent,Vector2 size){var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.sizeDelta=size;return rect;}
    private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private static void Top(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);}
    private static void Center(RectTransform rect,float x,float y,float w,float h){Top(rect,x,y,w,h);rect.pivot=Vector2.one*0.5f;}
    public static Sprite Sprite(string name){var path=AssetDatabase.FindAssets(name+" t:Texture2D",new[]{"Assets/Art"}).Select(AssetDatabase.GUIDToAssetPath).Single(p=>Path.GetFileNameWithoutExtension(p)==name);return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();}
    private static Image Image(string name,Transform parent,Sprite sprite,string color="FFFFFF",bool sliced=true,bool raycast=false){var rect=Rect(name,parent,Vector2.zero);Stretch(rect);var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=Hex(color);image.type=sliced?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;image.raycastTarget=raycast;return image;}
    private static TMP_Text Text(string name,Transform parent,string value,TMP_FontAsset font,float size,string color="3A2618"){var rect=Rect(name,parent,new Vector2(400,40));var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.text=value;text.font=font;text.fontSize=size;text.color=Hex(color);text.raycastTarget=false;text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.NoWrap;return text;}
    private static void Fit(TMP_Text text,float min){text.enableAutoSizing=true;text.fontSizeMax=text.fontSize;text.fontSizeMin=min;}
    private static GameObject Common(string name,Transform parent){return (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UICommonPrefabBuilder.Folder+name+".prefab"),parent);}
    public static void Bind(UnityEngine.Object owner,string field,UnityEngine.Object value){var so=new SerializedObject(owner);var property=so.FindProperty(field);if(property==null)throw new Exception("필드 없음: "+field);property.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    private static void Array(UnityEngine.Object owner,string field,IEnumerable<UnityEngine.Object> values){var so=new SerializedObject(owner);var p=so.FindProperty(field);var list=values.ToArray();p.arraySize=list.Length;for(int i=0;i<list.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=list[i];so.ApplyModifiedPropertiesWithoutUndo();}
    public static T Field<T>(UnityEngine.Object owner,string name) where T:UnityEngine.Object{return (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;}
    private static GameObject Frame<T>(float w,float h,string title,string description,bool gold=false,bool close=true) where T:UIBase
    {
        var root=Common("PopupFrame",null);PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.name=typeof(T).Name;Stretch(root.GetComponent<RectTransform>());var panel=root.AddComponent<T>();
        var so=new SerializedObject(panel);so.FindProperty("layer").enumValueIndex=(int)UILayer.Popup;so.ApplyModifiedPropertiesWithoutUndo();
        var body=root.transform.Find("Popup").GetComponent<RectTransform>();body.sizeDelta=new Vector2(w,h);
        var t=body.Find("Title").GetComponent<TMP_Text>();t.text=title;t.rectTransform.sizeDelta=new Vector2(gold?w-450:w-160,60);
        var sub=body.Find("Description").GetComponent<TMP_Text>();sub.text=description;sub.rectTransform.sizeDelta=new Vector2(w-112,36);
        var x=body.Find("CloseButton");x.GetComponent<RectTransform>().anchoredPosition=new Vector2(-56,-58);x.gameObject.SetActive(close);
        var goldRoot=body.Find("Gold");goldRoot.gameObject.SetActive(gold);goldRoot.GetComponent<RectTransform>().anchoredPosition=new Vector2(-366,-20);
        if(gold)Bind(panel,"goldText",goldRoot.Find("Amount").GetComponent<TMP_Text>());
        if(close)Bind(panel,"closeButton",x.GetComponentInChildren<Button>());
        root.GetComponent<PopupPresentation>().Configure(body,root.GetComponent<CanvasGroup>(),null);return root;
    }
    private static Button Action(string common,string name,Transform parent,float x,float y,float w,float h,string text,float font,bool glow=false)
    {var root=Common(common,parent);root.name=name;Top(root.GetComponent<RectTransform>(),x,y,w,h);var button=root.GetComponentInChildren<Button>();var label=button.transform.Find("Label").GetComponent<TMP_Text>();label.text=text;label.fontSize=font;label.rectTransform.sizeDelta=new Vector2(w-20,h-12);root.GetComponentInChildren<ButtonPresentation>().SetGlowVisible(glow);return button;}
    private static Image Pixel(string name,Transform parent,string id,float x,float y,float size)
    {var image=Image(name,parent,Resources.Load<Sprite>(ProfileIconCatalog.SpritePath(id)),sliced:false);Center(image.rectTransform,x,y,size,size);image.preserveAspect=true;image.gameObject.AddComponent<PixelIconSampling>();return image;}
    private static void Spark(Transform body,float x,float y,float size){var image=Image("Sparkle",body,Sprite("fx_sparkle"),"E0950F",false);Center(image.rectTransform,x,y,size,size);}
    private static TMP_Text Status(UIBase panel,Transform body,float x,float y,float width,float size=20,string color="A88E6E")
    {var text=Text("Status",body,"",Medium,size,color);Center(text.rectTransform,x,y,width,48);Fit(text,16);Bind(panel,"statusText",text);return text;}
    private static ScrollRect Scroll(string name,Transform parent,float x,float y,float w,float h,out RectTransform content)
    {
        var root=Rect(name,parent,new Vector2(w,h));Top(root,x,y,w,h);var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
        var viewport=Rect("Viewport",root,Vector2.zero);Stretch(viewport);var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
        content=Rect("Content",viewport,new Vector2(w,0));content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(0.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
        var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport;scroll.content=content;
        var bar=Rect("Scrollbar",root,new Vector2(10,h));Top(bar,w+8,0,10,h);var track=bar.gameObject.AddComponent<Image>();track.sprite=Sprite("slot_well");track.type=UnityEngine.UI.Image.Type.Sliced;
        var thumb=Image("Handle",bar,Sprite("pill_fill"),"CBB08A");var scrollbar=bar.gameObject.AddComponent<Scrollbar>();scrollbar.targetGraphic=thumb;scrollbar.handleRect=thumb.rectTransform;scrollbar.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=scrollbar;return scroll;
    }
    private static GameObject Gacha()
    {
        var root=Frame<GachaPanel>(1040,720,"프로필 아이콘 뽑기","골드로 새 프로필 아이콘을 뽑아요",true);var panel=root.GetComponent<GachaPanel>();var body=root.transform.Find("Popup");Bind(panel,"profileIconsJson",AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/profile_icons.json"));
        var stage=Image("Stage",body,Sprite("slot_well"));Top(stage.rectTransform,56,160,928,330);string[] ids={"icon_008","icon_012","icon_016","icon_020","icon_022"};for(int i=0;i<5;i++){var icon=Pixel("Preview"+i,body,ids[i],180+i*170,300,i==2?128:96);if(i!=2)icon.color=new Color(.8f,.8f,.8f,.55f);}
        var frame=Image("SelectedFrame",body,Sprite("profile_px_select"),"E0950F",false);Center(frame.rectTransform,520,300,140,140);
        Spark(body,390,200,26);Spark(body,680,214,20);Spark(body,300,400,16);Spark(body,760,404,22);
        int count=JsonUtility.FromJson<ProfileIconDefinitionFile>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/profile_icons.json").text).icons.Length;
        var info=Text("Info",body,count+"종 중 하나가 나와요 · 이미 가진 아이콘이 나오면 개수가 올라가요",Medium,21,"7A5C3E");Center(info.rectTransform,520,440,900,44);
        var draw=Action("PrimaryButton","Draw",body,300,540,440,104,"뽑기 (200골드)",40,true);Bind(panel,"drawButton",draw);Bind(panel,"drawButtonLabel",draw.transform.Find("Label").GetComponent<TMP_Text>());Status(panel,body,520,676,900);return root;
    }
    private static GameObject GachaResult()
    {
        var root=Frame<GachaResultView>(600,640,"뽑기 결과","",close:false);root.transform.Find("Dim").GetComponent<Image>().color=new Color(58/255f,38/255f,24/255f,.43f);var panel=root.GetComponent<GachaResultView>();var body=root.transform.Find("Popup");
        var glow=Image("ResultGlow",body,Sprite("fx_glow_round"),"F6C764",false);Center(glow.rectTransform,300,300,400,400);var color=glow.color;color.a=.6f;glow.color=color;
        Spark(body,150,190,26);Spark(body,450,210,22);Spark(body,440,380,16);Spark(body,160,392,18);
        var icon=Pixel("Icon",body,"icon_016",300,300,192);Bind(panel,"iconRoot",icon.rectTransform);Bind(panel,"iconImage",icon);
        var name=Text("Name",body,"스프린터",Jua,44);Center(name.rectTransform,300,440,500,58);Bind(panel,"nameText",name);
        var pill=Image("ResultPill",body,Sprite("pill_fill"),"E0950F");Center(pill.rectTransform,300,493,300,40);var result=Text("Result",pill.transform,"NEW! 새 아이콘을 얻었어요",Bold,19);Stretch(result.rectTransform);Fit(result,15);Bind(panel,"resultText",result);Bind(panel,"resultPill",pill);
        Bind(panel,"confirmButton",Action("PrimaryButton","Confirm",body,80,540,440,80,"확인",36));return root;
    }
    private static GameObject Compact()
    {
        var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(UIAugmentPrefabBuilder.CardPath));root.name="AugmentCardView_Compact";root.GetComponent<RectTransform>().sizeDelta=new Vector2(250,360);var layout=root.GetComponent<LayoutElement>();layout.minWidth=layout.preferredWidth=250;layout.minHeight=layout.preferredHeight=360;root.GetComponent<Button>().interactable=false;
        var visual=root.transform.Find("Visual");Center(visual.Find("Icon").GetComponent<RectTransform>(),125,86,128,128);var name=visual.Find("Name").GetComponent<TMP_Text>();name.fontSize=34;Center(name.rectTransform,125,178,230,52);Fit(name,28);
        var pill=visual.Find("CategoryPill").GetComponent<RectTransform>();Center(pill,125,219,76,30);pill.Find("Label").GetComponent<TMP_Text>().fontSize=18;
        var desc=visual.Find("Description").GetComponent<TMP_Text>();Top(desc.rectTransform,18,248,214,90);desc.fontSize=desc.fontSizeMax=19;desc.fontSizeMin=15;return root;
    }
    private static GameObject Collection()
    {
        var root=Frame<CollectionPanel>(1680,940,"증강 도감","직업 11종의 효과를 확인하세요");var panel=root.GetComponent<CollectionPanel>();var body=root.transform.Find("Popup");var grid=Rect("Grid",body,new Vector2(1568,744));Top(grid,56,160,1568,744);var layout=grid.gameObject.AddComponent<GridLayoutGroup>();layout.cellSize=new Vector2(250,360);layout.spacing=new Vector2(16,24);layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=6;
        Bind(panel,"augmentsJson",AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/augments.json"));Bind(panel,"augmentCardViewPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(Items+"AugmentCardView_Compact.prefab").GetComponent<AugmentCardView>());Bind(panel,"augmentCardContainer",grid);return root;
    }
    private static GameObject IconOption()
    {
        var root=Rect("ProfileIconOption",null,new Vector2(134,176));var paper=Image("Paper",root,Sprite("panel_paper"),raycast:true);var button=root.gameObject.AddComponent<Button>();button.targetGraphic=paper;button.transition=Selectable.Transition.None;button.navigation=new Navigation{mode=Navigation.Mode.None};
        Pixel("Icon",root,"icon_001",67,62,96);var frame=Image("SelectedFrame",root,Sprite("profile_px_select"),"E0950F",false);Center(frame.rectTransform,67,62,108,108);frame.gameObject.SetActive(false);
        var locked=Image("Lock",root,Sprite("badge_lock"),sliced:false);Center(locked.rectTransform,67,62,36,36);locked.gameObject.SetActive(false);
        var name=Text("IconName",root,"윷가락",Bold,18);Center(name.rectTransform,67,136,128,26);Fit(name,14);var state=Text("SelectionState",root,"선택",Medium,16,"7A5C3E");Center(state.rectTransform,67,158,128,24);
        root.gameObject.AddComponent<ProfileIconOptionPresentation>().Configure(frame,locked,state);return root.gameObject;
    }
    private static GameObject Profile()
    {
        var root=Frame<ProfilePanel>(1560,900,"내 정보","",true);var panel=root.GetComponent<ProfilePanel>();var body=root.transform.Find("Popup");var well=Image("LeftWell",body,Sprite("slot_well"));Top(well.rectTransform,56,130,420,700);
        var plate=Image("AvatarPlate",well.transform,Sprite("pill_fill"));Top(plate.rectTransform,82,40,256,256);var icon=Pixel("Avatar",well.transform,"icon_016",210,168,192);Bind(panel,"profileIcon",icon);
        var placeholder=Text("Placeholder",well.transform,"?",Jua,64,"CBB08A");Center(placeholder.rectTransform,210,168,192,192);Bind(panel,"iconPlaceholder",placeholder);
        var nickname=Text("Nickname",well.transform,"윷잘알",Jua,48);Center(nickname.rectTransform,210,360,380,64);Fit(nickname,24);Bind(panel,"nicknameText",nickname);
        var playerId=Text("PlayerId",well.transform,"abcd…wxyz",Medium,19,"A88E6E");Center(playerId.rectTransform,210,410,380,32);Bind(panel,"playerIdText",playerId);Bind(panel,"logoutButton",Action("SecondaryButton","Logout",well.transform,60,600,300,72,"로그아웃",30));
        var title=Text("GridTitle",body,"프로필 아이콘 선택",Jua,32);Top(title.rectTransform,516,130,800,44);title.alignment=TextAlignmentOptions.MidlineLeft;
        RectTransform content;var scroll=Scroll("IconScroll",body,516,194,1000,620,out content);var grid=content.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(134,176);grid.spacing=new Vector2(10,14);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=7;
        Bind(panel,"iconScrollRect",scroll);Bind(panel,"iconOptionContainer",content);Bind(panel,"iconOptionTemplate",AssetDatabase.LoadAssetAtPath<GameObject>(Items+"ProfileIconOption.prefab").GetComponent<Button>());Bind(panel,"profileIconsJson",AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/profile_icons.json"));
        var hint=Text("Hint",body,"아이콘을 누르면 바로 바뀌어요",Medium,20,"A88E6E");Center(hint.rectTransform,1016,834,980,26);Status(panel,body,1016,870,980);return root;
    }
    private static GameObject AchievementItem()
    {
        var root=Common("PaperCard",null);PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.name="AchievementItemView";root.GetComponent<RectTransform>().sizeDelta=new Vector2(1168,140);var view=root.AddComponent<AchievementItemView>();var layout=root.AddComponent<LayoutElement>();layout.minHeight=layout.preferredHeight=140;layout.minWidth=layout.preferredWidth=1168;layout.flexibleHeight=layout.flexibleWidth=0;
        var border=root.transform.Find("EmphasisBorder").GetComponent<Image>();border.color=Hex("E0950F");Bind(view,"claimableBorder",border);
        var name=Text("Name",root.transform,"첫 출석",Jua,34);Center(name.rectTransform,246,42,420,46);name.alignment=TextAlignmentOptions.MidlineLeft;Fit(name,22);Bind(view,"nameText",name);
        var desc=Text("Description",root.transform,"하루 접속하기",Medium,20,"7A5C3E");Center(desc.rectTransform,246,82,420,38);desc.alignment=TextAlignmentOptions.MidlineLeft;Fit(desc,15);Bind(view,"descriptionText",desc);
        var track=Image("Track",root.transform,Sprite("slot_well"));Top(track.rectTransform,470,56,330,22);var fill=Image("Fill",root.transform,Sprite("pill_fill"),"8A5A2E");Top(fill.rectTransform,470,56,330,22);fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;Bind(view,"progressFill",fill);
        var progress=Text("Progress",root.transform,"1 / 1",Bold,20,"7A5C3E");Center(progress.rectTransform,856,66,76,30);Fit(progress,13);Bind(view,"progressText",progress);
        var coin=Image("Coin",root.transform,Sprite("icon_coin"),sliced:false);Center(coin.rectTransform,910,68,36,36);var reward=Text("Reward",root.transform,"200 골드",Bold,20);Top(reward.rectTransform,934,51,80,30);reward.alignment=TextAlignmentOptions.MidlineLeft;Fit(reward,13);Bind(view,"rewardText",reward);
        var claim=Action("PrimaryButton","Claim",root.transform,1020,36,128,68,"받기",28);UnityEngine.Object.DestroyImmediate(claim.GetComponent<ButtonPresentation>());Bind(view,"claimButton",claim);Bind(view,"claimButtonLabel",claim.transform.Find("Label").GetComponent<TMP_Text>());
        var check=Image("ClaimedCheck",claim.transform,Sprite("icon_check"),"7A5C3E",false);Center(check.rectTransform,23,34,22,22);check.gameObject.SetActive(false);Bind(view,"claimedCheck",check.gameObject);Bind(view,"claimReadySprite",Sprite("btn_throw_normal"));Bind(view,"claimBusySprite",Sprite("btn_throw_disabled"));Bind(view,"claimDisabledSprite",Sprite("btn_rethrow_disabled"));return root;
    }
    private static GameObject Achievement()
    {
        var root=Frame<AchievementPanel>(1280,900,"업적","업적을 달성하면 보상을 받을 수 있습니다.",true);var panel=root.GetComponent<AchievementPanel>();RectTransform content;var scroll=Scroll("Achievements",root.transform.Find("Popup"),56,160,1168,660,out content);var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=24;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=false;
        Bind(panel,"scrollRect",scroll);Bind(panel,"itemContainer",content);Bind(panel,"itemPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(Items+"AchievementItemView.prefab").GetComponent<AchievementItemView>());Status(panel,root.transform.Find("Popup"),640,840,1168,22,"B36B00");return root;
    }
    private static GameObject Settings()
    {
        var root=Frame<SettingsPanel>(800,640,"설정","소리 크기를 조절할 수 있습니다.");var panel=root.GetComponent<SettingsPanel>();var body=root.transform.Find("Popup");string[] keys={"master","bgm","sfx"},labels={"전체 음량","배경 음악","효과음"};
        for(int i=0;i<3;i++)
        {
            float y=208+i*110;var label=Text("Label"+i,body,labels[i],Jua,30);Center(label.rectTransform,148,y,184,44);label.alignment=TextAlignmentOptions.MidlineLeft;
            var sliderRoot=Rect(keys[i]+"Slider",body,new Vector2(380,44));Top(sliderRoot,250,y-22,380,44);var slider=sliderRoot.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;
            var background=Image("Background",sliderRoot,Sprite("slot_well"),raycast:true);Top(background.rectTransform,0,10,380,24);
            var area=Rect("FillArea",sliderRoot,Vector2.zero);Stretch(area);area.offsetMin=new Vector2(0,10);area.offsetMax=new Vector2(0,-10);var fill=Image("Fill",area,Sprite("pill_fill"),"8A5A2E");
            var handleArea=Rect("HandleArea",sliderRoot,Vector2.zero);Stretch(handleArea);var handle=Image("Handle",handleArea,Sprite("pill_fill"));handle.rectTransform.anchorMin=new Vector2(.5f,0);handle.rectTransform.anchorMax=new Vector2(.5f,1);handle.rectTransform.sizeDelta=new Vector2(44,0);Image("Border",handle.transform,Sprite("btn_round_border"),"8A5A2E",false);
            slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;Bind(panel,keys[i]+"Slider",slider);
            var value=Text("Value"+i,body,"100%",Jua,28,"7A5C3E");Center(value.rectTransform,700,y,88,44);value.alignment=TextAlignmentOptions.MidlineRight;Bind(panel,keys[i]+"ValueText",value);
        }
        Bind(panel,"exitGameButton",Action("SecondaryButton","Exit",body,200,530,400,76,"메인 화면으로 나가기",28));return root;
    }
    private static GameObject Reconnect()
    {
        var root=Frame<ReconnectPanel>(760,560,"게임 재연결","",close:false);var panel=root.GetComponent<ReconnectPanel>();var body=root.transform.Find("Popup");var a=Text("Body1",body,"연결이 끊긴 경기가 있습니다.",Medium,24);Center(a.rectTransform,380,170,650,36);var b=Text("Body2",body,"서버에서 복귀 가능한지 확인합니다.",Medium,22,"7A5C3E");Center(b.rectTransform,380,206,650,36);
        var well=Image("RoomWell",body,Sprite("slot_well"));Top(well.rectTransform,100,250,560,96);var label=Text("Label",well.transform,"이전에 참가한 방",Bold,20,"7A5C3E");Center(label.rectTransform,142,48,204,36);
        var room=Text("Room",body,"K7Q2MX",Jua,40);Center(room.rectTransform,500,296,240,56);room.alignment=TextAlignmentOptions.MidlineRight;Bind(panel,"roomText",room);var status=Status(panel,body,380,386,640);status.gameObject.AddComponent<InstructionStatusPresentation>().Configure(status,"연결이 끊긴 경기가 있습니다.\n서버에서 복귀 가능한지 확인합니다.");
        Bind(panel,"reconnectButton",Action("PrimaryButton","Reconnect",body,100,420,260,88,"재연결",36,true));Bind(panel,"closeButton",Action("SecondaryButton","Close",body,400,420,260,88,"닫기",36));return root;
    }
    [MenuItem("Tools/Yutnori/UIv2/Generate Popup Prefabs")]
    public static void Generate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Play를 종료해 주세요.");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            foreach(var pair in new Dictionary<string,Func<GameObject>>{{"ProfileIconOption",IconOption},{"AchievementItemView",AchievementItem},{"AugmentCardView_Compact",Compact}}){var root=pair.Value();SceneManager.MoveGameObjectToScene(root,scene);PrefabUtility.SaveAsPrefabAsset(root,Items+pair.Key+".prefab");UnityEngine.Object.DestroyImmediate(root);}
            foreach(var pair in new Dictionary<string,Func<GameObject>>{{"GachaPanel",Gacha},{"GachaResultView",GachaResult},{"CollectionPanel",Collection},{"ProfilePanel",Profile},{"AchievementPanel",Achievement},{"SettingsPanel",Settings},{"ReconnectPanel",Reconnect}}){var root=pair.Value();SceneManager.MoveGameObjectToScene(root,scene);PrefabUtility.SaveAsPrefabAsset(root,Folder+pair.Key+".prefab");UnityEngine.Object.DestroyImmediate(root);}
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    private static void Replace(UIManager manager,string file,List<string> rows,bool addMissing)
    {
        var so=new SerializedObject(manager);var list=so.FindProperty("prefabs");var found=new HashSet<string>();
        for(int i=0;i<list.arraySize;i++){var item=list.GetArrayElementAtIndex(i);var old=item.objectReferenceValue;if(old==null||!Names.Contains(old.GetType().Name))continue;string name=old.GetType().Name;if(!found.Add(name))throw new Exception("중복 등록 "+name);if(AssetDatabase.GetAssetPath(old)==Folder+name+".prefab")continue;rows.Add("| "+file+" | UIManager.prefabs["+i+"] | "+AssetDatabase.GetAssetPath(old)+" | "+Folder+name+".prefab |");item.objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab").GetComponent(old.GetType());}
        if(addMissing)foreach(string name in Names.Where(name=>!found.Contains(name))){int index=list.arraySize;list.arraySize++;var panel=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab").GetComponent<UIBase>();list.GetArrayElementAtIndex(index).objectReferenceValue=panel;rows.Add("| "+file+" | UIManager.prefabs["+index+"] | 미등록 (옛 프리팹은 Lobby 씬에 등록) | "+Folder+name+".prefab |");}
        so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
    }
    [MenuItem("Tools/Yutnori/UIv2/Replace Popup References")]
    public static void ReplaceReferences()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Play를 종료해 주세요.");var rows=new List<string>();var original=SceneManager.GetActiveScene();var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/UIRoot.prefab");
        try{Replace(root.GetComponentInChildren<UIManager>(true),"Assets/Prefabs/UI/UIRoot.prefab",rows,true);PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/UI/UIRoot.prefab");}finally{PrefabUtility.UnloadPrefabContents(root);}
        foreach(string path in new[]{"Assets/Scenes/Lobby.unity","Assets/Scenes/Game.unity"})
        {
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try{var manager=scene.GetRootGameObjects().SelectMany(item=>item.GetComponentsInChildren<UIManager>(true)).Single();Replace(manager,path,rows,false);EditorSceneManager.SaveScene(scene);}
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(original);}
        }
        Directory.CreateDirectory(UIScreenPreparation.Reports);if(rows.Count>0)File.WriteAllText(UIScreenPreparation.Reports+"stage7_reference_changes.md","# 7단계 참조 교체\n\n| 파일 | 필드 | 이전 | 이후 |\n|---|---|---|---|\n"+string.Join("\n",rows)+"\n\nGame 씬의 SettingsPanel은 UIRoot[5]를 상속합니다. 새 참조를 확인했으며 별도 Game 씬 수정은 필요하지 않습니다.\n");
    }
}
