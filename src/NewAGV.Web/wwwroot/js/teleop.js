window.teleop = {
  _dotNetRef: null,
  _pressedKeys: new Set(),
  _active: false,
  _intervalId: null,
  _intervalMs: 125,

  start: function (dotNetRef, intervalMs) {
    this.stop();
    this._dotNetRef = dotNetRef;
    this._active = true;
    this._intervalMs = intervalMs || 125;
    this._pressedKeys.clear();
    document.addEventListener("keydown", this._onKeyDown);
    document.addEventListener("keyup", this._onKeyUp);
    window.addEventListener("blur", this._onBlur);
    document.addEventListener("visibilitychange", this._onVisibilityChange);
    this._intervalId = window.setInterval(this._publishState, this._intervalMs);
    this._publishState();
  },

  stop: function () {
    this._active = false;
    document.removeEventListener("keydown", this._onKeyDown);
    document.removeEventListener("keyup", this._onKeyUp);
    window.removeEventListener("blur", this._onBlur);
    document.removeEventListener("visibilitychange", this._onVisibilityChange);
    if (this._intervalId) {
      window.clearInterval(this._intervalId);
      this._intervalId = null;
    }
    this._pressedKeys.clear();
    this._publishState();
    this._dotNetRef = null;
  },

  setPointerKey: function (key, isPressed) {
    if (!this._active) return;
    var normalizedKey = (key || "").toLowerCase();
    if (["w", "a", "s", "d"].indexOf(normalizedKey) === -1) return;
    if (isPressed) {
      this._pressedKeys.add(normalizedKey);
    } else {
      this._pressedKeys.delete(normalizedKey);
    }
    this._publishState();
  },

  _onKeyDown: function (e) {
    if (!window.teleop._active) return;
    var key = e.key.toLowerCase();
    if (["w", "a", "s", "d", " "].indexOf(key) !== -1) {
      e.preventDefault();
      window.teleop._pressedKeys.add(key);
      window.teleop._publishState();
    }
  },

  _onKeyUp: function (e) {
    if (!window.teleop._active) return;
    var key = e.key.toLowerCase();
    if (["w", "a", "s", "d", " "].indexOf(key) !== -1) {
      e.preventDefault();
      window.teleop._pressedKeys.delete(key);
      window.teleop._publishState();
    }
  },

  _onBlur: function () {
    if (!window.teleop._active) return;
    window.teleop._pressedKeys.clear();
    window.teleop._publishState();
  },

  _onVisibilityChange: function () {
    if (!window.teleop._active) return;
    if (document.hidden) {
      window.teleop._pressedKeys.clear();
      window.teleop._publishState();
    }
  },

  _publishState: function () {
    if (!window.teleop._dotNetRef) return;
    var keyW = window.teleop._pressedKeys.has("w");
    var keyA = window.teleop._pressedKeys.has("a");
    var keyS = window.teleop._pressedKeys.has("s");
    var keyD = window.teleop._pressedKeys.has("d");
    var vx = (window.teleop._pressedKeys.has("w") ? 1 : 0) - (window.teleop._pressedKeys.has("s") ? 1 : 0);
    var az = (window.teleop._pressedKeys.has("a") ? 1 : 0) - (window.teleop._pressedKeys.has("d") ? 1 : 0);
    var stop = window.teleop._pressedKeys.has(" ");
    window.teleop._dotNetRef.invokeMethodAsync(
      "OnTeleopKeyboardStateChanged",
      stop ? 0 : vx,
      stop ? 0 : az,
      keyW,
      keyA,
      keyS,
      keyD
    );
  }
};
