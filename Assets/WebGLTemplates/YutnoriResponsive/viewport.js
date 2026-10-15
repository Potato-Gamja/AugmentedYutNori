(function (root) {
  "use strict";
  // UI anchors and the world camera use the same 16:9 canvas, including in an iframe.
  function fit(width, height) {
    var fittedWidth = Math.max(0, Math.min(width, height * 16 / 9));
    return { width: fittedWidth, height: fittedWidth * 9 / 16 };
  }
  function mount(canvas, container) {
    function resize() {
      var style = getComputedStyle(container);
      var width = container.clientWidth - parseFloat(style.paddingLeft || 0) - parseFloat(style.paddingRight || 0);
      var height = container.clientHeight - parseFloat(style.paddingTop || 0) - parseFloat(style.paddingBottom || 0);
      var size = fit(width, height);
      canvas.style.width = size.width + "px";
      canvas.style.height = size.height + "px";
    }
    window.addEventListener("resize", resize);
    document.addEventListener("fullscreenchange", resize);
    document.addEventListener("webkitfullscreenchange", resize);
    if (window.visualViewport) window.visualViewport.addEventListener("resize", resize);
    if (typeof ResizeObserver !== "undefined") new ResizeObserver(resize).observe(container);
    resize();
  }
  var api = { fit: fit, mount: mount };
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  else root.YutnoriViewport = api;
})(typeof globalThis !== "undefined" ? globalThis : this);
