mergeInto(LibraryManager.library, {
  $YutNickname: {
    input: null,
    composing: false,
    endedAt: -1000,
    receiver: '',
    limit: 20,
    send: function (method, value) {
      SendMessage(YutNickname.receiver, method, value);
    },
    commit: function () {
      var input = YutNickname.input;
      if (!input || YutNickname.composing) return;
      // Limit only after composition ends; never cut an unfinished Korean syllable.
      input.value = input.value.substring(0, YutNickname.limit);
      YutNickname.send('OnBrowserNicknameChanged', input.value);
    },
    close: function () {
      if (!YutNickname.input) return;
      YutNickname.input.blur();
      YutNickname.input.remove();
      YutNickname.input = null;
      YutNickname.composing = false;
    }
  },
  Yut_NicknameOpen__deps: ['$YutNickname'],
  Yut_NicknameOpen: function (receiver, value, placeholder, limit) {
    YutNickname.close();
    YutNickname.receiver = UTF8ToString(receiver);
    YutNickname.limit = limit;
    YutNickname.endedAt = -1000;
    var input = document.createElement('input');
    input.type = 'text';
    input.value = UTF8ToString(value);
    input.placeholder = UTF8ToString(placeholder);
    input.setAttribute('aria-label', '닉네임');
    input.autocomplete = 'off';
    input.spellcheck = false;
    input.setAttribute('autocapitalize', 'off');
    input.enterKeyHint = 'done';
    Object.assign(input.style, {
      position: 'fixed', zIndex: '2147483647', boxSizing: 'border-box',
      margin: '0', padding: '0', border: '0', outline: 'none', borderRadius: '0',
      background: 'transparent', color: '#3A2618', fontFamily: 'sans-serif',
      minWidth: '0', minHeight: '0', display: 'none'
    });
    input.addEventListener('focus', function () { YutNickname.send('OnBrowserNicknameFocus', '1'); });
    input.addEventListener('blur', function () { YutNickname.send('OnBrowserNicknameFocus', '0'); });
    input.addEventListener('compositionstart', function () {
      YutNickname.composing = true;
      YutNickname.send('OnBrowserNicknameComposition', '1');
    });
    input.addEventListener('compositionend', function () {
      YutNickname.composing = false;
      YutNickname.endedAt = performance.now();
      YutNickname.commit();
      YutNickname.send('OnBrowserNicknameComposition', '0');
    });
    input.addEventListener('input', function (event) {
      if (!event.isComposing) YutNickname.commit();
    });
    input.addEventListener('keydown', function (event) {
      event.stopPropagation();
      if (event.key !== 'Enter') return;
      if (event.isComposing || YutNickname.composing || event.keyCode === 229 ||
          performance.now() - YutNickname.endedAt < 50) return;
      event.preventDefault();
      if (!input.disabled) {
        YutNickname.commit();
        YutNickname.send('OnBrowserNicknameSubmit', input.value);
      }
    });
    input.addEventListener('keyup', function (event) { event.stopPropagation(); });
    YutNickname.input = input;
    var host = document.fullscreenElement || document.webkitFullscreenElement;
    if (!host || host.tagName === 'CANVAS') host = document.body;
    host.appendChild(input);
    // Do not focus from an async callback: tapping the real input also opens mobile keyboards.
  },
  Yut_NicknameRect__deps: ['$YutNickname'],
  Yut_NicknameRect: function (x, y, width, height, fontSize) {
    var input = YutNickname.input;
    if (!input) return;
    var canvas = Module.canvas;
    var rect = canvas.getBoundingClientRect();
    var host = document.fullscreenElement || document.webkitFullscreenElement;
    if (!host || host.tagName === 'CANVAS') host = document.body;
    if (input.parentNode !== host) host.appendChild(input);
    Object.assign(input.style, {
      left: (rect.left + x * rect.width) + 'px', top: (rect.top + y * rect.height) + 'px',
      width: Math.max(0, width * rect.width) + 'px', height: Math.max(0, height * rect.height) + 'px',
      fontSize: Math.max(16, fontSize * rect.height) + 'px',
      display: width > 0 && height > 0 ? 'block' : 'none'
    });
  },
  Yut_NicknameSetValue__deps: ['$YutNickname'],
  Yut_NicknameSetValue: function (value) {
    if (YutNickname.input && !YutNickname.composing) YutNickname.input.value = UTF8ToString(value);
  },
  Yut_NicknameSetEnabled__deps: ['$YutNickname'],
  Yut_NicknameSetEnabled: function (enabled) {
    if (YutNickname.input) YutNickname.input.disabled = !enabled;
  },
  Yut_NicknameClose__deps: ['$YutNickname'],
  Yut_NicknameClose: function () { YutNickname.close(); }
});
