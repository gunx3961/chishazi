window.chishaziAmap = {
  openPoiSearch(androidUrl, iosUrl, webUrl) {
    const userAgent = navigator.userAgent || "";
    const isAndroid = /Android/i.test(userAgent);
    const isIos = /iPhone|iPad|iPod/i.test(userAgent);
    const targetUrl = isAndroid ? androidUrl : isIos ? iosUrl : webUrl;

    if (!isAndroid && !isIos) {
      window.open(webUrl, "_blank", "noopener,noreferrer");
      return;
    }

    let leftPage = false;
    const markLeftPage = () => {
      leftPage = true;
    };
    const markHidden = () => {
      if (document.hidden) {
        markLeftPage();
      }
    };

    window.addEventListener("blur", markLeftPage, { once: true });
    window.addEventListener("pagehide", markLeftPage, { once: true });
    document.addEventListener("visibilitychange", markHidden, { once: true });

    window.location.href = targetUrl;

    window.setTimeout(() => {
      window.removeEventListener("blur", markLeftPage);
      window.removeEventListener("pagehide", markLeftPage);
      document.removeEventListener("visibilitychange", markHidden);

      if (!leftPage) {
        window.open(webUrl, "_blank", "noopener,noreferrer");
      }
    }, 900);
  }
};
