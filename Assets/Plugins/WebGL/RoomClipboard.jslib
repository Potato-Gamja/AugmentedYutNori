mergeInto(LibraryManager.library, {
  Yut_CopyRoomCode: function (codePtr, receiverPtr) {
    var code = UTF8ToString(codePtr);
    var receiver = UTF8ToString(receiverPtr);
    var report = function (success) {
      SendMessage(receiver, success ? 'OnRoomCodeCopySucceeded' : 'OnRoomCodeCopyFailed');
    };
    var fallback = function () {
      var previous = document.activeElement;
      var input = document.createElement('textarea');
      input.value = code;
      input.readOnly = true;
      input.style.position = 'fixed';
      input.style.opacity = '0';
      document.body.appendChild(input);
      var success = false;
      try {
        input.focus();
        input.select();
        success = document.execCommand('copy');
      } catch (error) {
        success = false;
      } finally {
        document.body.removeChild(input);
        if (previous && previous.focus) previous.focus();
      }
      report(success);
    };
    if (navigator.clipboard && navigator.clipboard.writeText) {
      try {
        navigator.clipboard.writeText(code).then(function () { report(true); }, fallback);
      } catch (error) {
        fallback();
      }
    } else {
      fallback();
    }
  }
});
