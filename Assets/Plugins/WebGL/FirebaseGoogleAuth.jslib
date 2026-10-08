// WebGLGoogleSignIn.cs의 JS 쪽. Firebase JS SDK(compat)를 CDN에서 불러와 구글 팝업 로그인을 한다.
// 결과는 SendMessage(receiverName, 메서드, 문자열)로 C#에 돌려준다.
mergeInto(LibraryManager.library, {

  FirebaseGoogleAuth_Init: function (configJsonPtr, receiverNamePtr) {
    var config = JSON.parse(UTF8ToString(configJsonPtr));
    var receiver = UTF8ToString(receiverNamePtr);
    var sdkVersion = "10.12.2";

    var state = window.__yutFirebaseAuth = window.__yutFirebaseAuth || {};
    state.receiver = receiver;
    state.send = function (method, value) { SendMessage(state.receiver, method, value || ""); };

    if (state.auth) { state.send("OnFirebaseReady"); return; }

    function load(src) {
      return new Promise(function (resolve, reject) {
        var script = document.createElement("script");
        script.src = src;
        script.onload = resolve;
        script.onerror = function () { reject(new Error("스크립트를 불러오지 못했습니다: " + src)); };
        document.head.appendChild(script);
      });
    }

    var base = "https://www.gstatic.com/firebasejs/" + sdkVersion + "/";
    load(base + "firebase-app-compat.js")
      .then(function () { return load(base + "firebase-auth-compat.js"); })
      .then(function () {
        if (!firebase.apps.length) firebase.initializeApp(config);
        state.auth = firebase.auth();
        state.send("OnFirebaseReady");
      })
      .catch(function (e) { state.send("OnFirebaseInitFailed", e && e.message ? e.message : String(e)); });
  },

  // 버튼을 누른 순간 호출된다. 손을 떼는 브라우저 이벤트 안에서 팝업을 열어야 팝업 차단에 걸리지 않는다
  FirebaseGoogleAuth_ArmPopup: function () {
    var state = window.__yutFirebaseAuth;
    if (!state || !state.auth) {
      if (state) state.send("OnGoogleSignInFailed", JSON.stringify({ code: "not-ready", message: "Firebase가 아직 준비되지 않았습니다." }));
      return;
    }
    if (state.armed) return;
    state.armed = true;

    var openPopup = function () {
      window.removeEventListener("pointerup", openPopup, true);
      state.armed = false;

      var provider = new firebase.auth.GoogleAuthProvider();
      provider.setCustomParameters({ prompt: "select_account" });

      state.auth.signInWithPopup(provider)
        .then(function (credential) {
          var user = credential.user;
          return user.getIdToken().then(function (idToken) {
            state.send("OnGoogleSignInSucceeded", JSON.stringify({
              idToken: idToken,
              uid: user.uid,
              email: user.email,
              displayName: user.displayName
            }));
          });
        })
        .catch(function (e) {
          state.send("OnGoogleSignInFailed", JSON.stringify({ code: e.code || "", message: e.message || String(e) }));
        });
    };

    window.addEventListener("pointerup", openPopup, true);
  }
});
