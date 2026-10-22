mergeInto(LibraryManager.library, {
  $YutRoomClipboard: {
    dialog: null,
    request: 0,
    closeDialog: function () {
      var state = YutRoomClipboard.dialog;
      if (!state) return;
      YutRoomClipboard.dialog = null;
      if (state.element.open && state.element.close) state.element.close();
      state.element.remove();
      SendMessage(state.receiver, 'OnRoomCodeCopyFocus', '0');
      if (state.previous && state.previous.isConnected) state.previous.focus();
    },
    host: function () {
      var full = document.fullscreenElement || document.webkitFullscreenElement;
      // A native modal dialog uses the top layer even if the canvas itself is fullscreen.
      return full && full.tagName !== 'CANVAS' ? full : document.body;
    },
    legacyCopy: function (code) {
      var previous = document.activeElement;
      var input = document.createElement('textarea');
      input.value = code;
      input.readOnly = true;
      Object.assign(input.style, { position: 'fixed', opacity: '0', left: '0', top: '0', fontSize: '16px' });
      var modal = YutRoomClipboard.dialog;
      (modal ? modal.element : YutRoomClipboard.host()).appendChild(input);
      try {
        input.focus(); input.select(); input.setSelectionRange(0, input.value.length);
        return document.execCommand('copy') === true;
      } catch (_) { return false; }
      finally {
        input.remove();
        if (previous && previous.isConnected) previous.focus();
      }
    },
    attempt: function (code, done, current) {
      var fallback = function () {
        if (!current || current()) done(YutRoomClipboard.legacyCopy(code));
      };
      if (navigator.clipboard && navigator.clipboard.writeText) {
        try { navigator.clipboard.writeText(code).then(function () { done(true); }, fallback); }
        catch (_) { fallback(); }
      } else { fallback(); }
    },
    manual: function (code, receiver, request) {
      if (request !== YutRoomClipboard.request) return;
      var previous = document.activeElement;
      var dialog = document.createElement('dialog');
      dialog.setAttribute('aria-label', '방 코드 복사');
      Object.assign(dialog.style, {
        position: 'fixed', inset: '0', margin: 'auto', boxSizing: 'border-box',
        width: 'min(420px, calc(100vw - 32px))', height: 'fit-content', maxHeight: '90vh',
        overflow: 'auto', padding: '24px', border: '2px solid #CBB08A', borderRadius: '16px',
        background: '#FFF8ED', color: '#3A2618', fontFamily: 'sans-serif', zIndex: '2147483647'
      });
      var title = document.createElement('h2'); title.textContent = '방 코드 복사';
      title.style.margin = '0 0 12px'; title.style.fontSize = '22px';
      var message = document.createElement('p');
      message.textContent = '자동 복사가 제한되었습니다. 복사 버튼을 다시 누르거나 아래 코드를 선택해 복사해 주세요.';
      Object.assign(message.style, { fontSize: '16px', lineHeight: '1.6' });
      var input = document.createElement('input');
      input.type = 'text'; input.readOnly = true; input.value = code;
      input.setAttribute('aria-label', '방 코드');
      Object.assign(input.style, { width: '100%', boxSizing: 'border-box', fontSize: '28px',
        textAlign: 'center', padding: '12px', border: '1px solid #B7793F', borderRadius: '8px',
        background: '#FFFFFF', color: '#3A2618', userSelect: 'text', webkitUserSelect: 'text' });
      var select = function () { input.focus(); input.select(); input.setSelectionRange(0, input.value.length); };
      input.addEventListener('click', select);
      var hint = document.createElement('p'); hint.textContent = 'PC: Ctrl+C / Mac: ⌘C · 모바일: 코드를 길게 눌러 복사';
      Object.assign(hint.style, { fontSize: '14px', lineHeight: '1.6' });
      var actions = document.createElement('div');
      Object.assign(actions.style, { display: 'flex', gap: '12px', marginTop: '20px' });
      var copy = document.createElement('button'); copy.type = 'button'; copy.textContent = '복사';
      var close = document.createElement('button'); close.type = 'button'; close.textContent = '닫기';
      [copy, close].forEach(function (button) {
        Object.assign(button.style, { flex: '1', padding: '12px', fontSize: '16px', borderRadius: '8px',
          border: '1px solid #B7793F', background: '#FFFFFF', color: '#3A2618', cursor: 'pointer' });
        actions.appendChild(button);
      });
      [title, message, input, hint, actions].forEach(function (element) { dialog.appendChild(element); });
      var state = { element: dialog, receiver: receiver, previous: previous };
      YutRoomClipboard.dialog = state;
      close.addEventListener('click', function () { YutRoomClipboard.closeDialog(); });
      dialog.addEventListener('cancel', function (event) { event.preventDefault(); YutRoomClipboard.closeDialog(); });
      dialog.addEventListener('close', function () {
        if (YutRoomClipboard.dialog === state) YutRoomClipboard.closeDialog();
      });
      dialog.addEventListener('keydown', function (event) { event.stopPropagation(); });
      dialog.addEventListener('keyup', function (event) { event.stopPropagation(); });
      // The retry runs directly in a real DOM click handler, preserving user activation.
      copy.addEventListener('click', function () {
        if (copy.disabled) return;
        copy.disabled = true;
        YutRoomClipboard.attempt(code, function (success) {
          if (request !== YutRoomClipboard.request || YutRoomClipboard.dialog !== state) return;
          copy.disabled = false;
          if (success) {
            SendMessage(receiver, 'OnRoomCodeCopySucceeded');
            YutRoomClipboard.closeDialog();
          } else {
            message.textContent = '브라우저가 자동 복사를 허용하지 않습니다. 아래 코드를 선택해 직접 복사해 주세요.';
            select();
          }
        }, function () { return request === YutRoomClipboard.request && YutRoomClipboard.dialog === state; });
      });
      YutRoomClipboard.host().appendChild(dialog);
      SendMessage(receiver, 'OnRoomCodeCopyFocus', '1');
      try {
        if (dialog.showModal) dialog.showModal();
        else { dialog.setAttribute('open', ''); dialog.style.display = 'block'; }
        select();
        SendMessage(receiver, 'OnRoomCodeCopyManual');
      } catch (_) {
        YutRoomClipboard.closeDialog();
        SendMessage(receiver, 'OnRoomCodeCopyFailed');
      }
    }
  },
  Yut_CopyRoomCode__deps: ['$YutRoomClipboard'],
  Yut_CopyRoomCode: function (codePtr, receiverPtr) {
    var code = UTF8ToString(codePtr), receiver = UTF8ToString(receiverPtr);
    var request = ++YutRoomClipboard.request;
    YutRoomClipboard.closeDialog();
    YutRoomClipboard.attempt(code, function (success) {
      if (request !== YutRoomClipboard.request) return;
      if (success) SendMessage(receiver, 'OnRoomCodeCopySucceeded');
      else YutRoomClipboard.manual(code, receiver, request);
    }, function () { return request === YutRoomClipboard.request; });
  },
  Yut_CloseRoomCodeCopy__deps: ['$YutRoomClipboard'],
  Yut_CloseRoomCodeCopy: function () {
    YutRoomClipboard.request++;
    YutRoomClipboard.closeDialog();
  }
});
