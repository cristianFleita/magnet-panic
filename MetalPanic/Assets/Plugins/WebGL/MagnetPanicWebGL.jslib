mergeInto(LibraryManager.library, {
  MagnetPanicGetLeaderboardUrl: function () {
    var value = "";

    if (typeof window !== "undefined") {
      if (
        window.MAGNET_PANIC_CONFIG &&
        typeof window.MAGNET_PANIC_CONFIG.leaderboardUrl === "string"
      ) {
        value = window.MAGNET_PANIC_CONFIG.leaderboardUrl;
      } else if (typeof window.BACKEND_URL === "string") {
        value = window.BACKEND_URL;
      }
    }

    value = value.trim();
    var length = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(length);
    stringToUTF8(value, buffer, length);
    return buffer;
  }
});
