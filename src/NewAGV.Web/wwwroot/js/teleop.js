window.teleop = {
  _dotNetRef: null,
  _pressedKeys: new Set(),
  _active: false,

  start: function (dotNetRef) {
    this._dotNetRef = dotNetRef;
    this._active = true;
    this._pressedKeys.clear();
    document.addEventListener("keydown", this._onKeyDown);
    document.addEventListener("keyup", this._onKeyUp);
  },

  stop: function () {
    this._active = false;
    document.removeEventListener("keydown", this._onKeyDown);
    document.removeEventListener("keyup", this._onKeyUp);
    this._pressedKeys.clear();
    this._dotNetRef = null;
  },

  _onKeyDown: function (e) {
    if (!window.teleop._active) return;
    var key = e.key.toLowerCase();
    if (["w", "a", "s", "d", " "].indexOf(key) !== -1) {
      e.preventDefault();
      window.teleop._pressedKeys.add(key);
      window.teleop._sendVelocity();
    } else if (key === "e" || key === "q") {
      e.preventDefault();
      window.teleop._pressedKeys.add(key);
      window.teleop._sendVelocity();
    }
  },

  _onKeyUp: function (e) {
    if (!window.teleop._active) return;
    var key = e.key.toLowerCase();
    if (["w", "a", "s", "d", " ", "e", "q"].indexOf(key) !== -1) {
      e.preventDefault();
      window.teleop._pressedKeys.delete(key);
      window.teleop._sendVelocity();
    }
  },

  _sendVelocity: function () {
    if (!window.teleop._dotNetRef) return;
    var vx = (window.teleop._pressedKeys.has("w") ? 1 : 0) - (window.teleop._pressedKeys.has("s") ? 1 : 0);
    var vy = (window.teleop._pressedKeys.has("e") ? 1 : 0) - (window.teleop._pressedKeys.has("q") ? 1 : 0);
    var az = (window.teleop._pressedKeys.has("a") ? 1 : 0) - (window.teleop._pressedKeys.has("d") ? 1 : 0);
    var stop = window.teleop._pressedKeys.has(" ");
    window.teleop._dotNetRef.invokeMethodAsync("OnTeleopVelocity", stop ? 0 : vx, stop ? 0 : vy, stop ? 0 : az);
  }
};
