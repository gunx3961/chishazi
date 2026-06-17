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

    let appOpened = false;
    const markAppOpened = () => {
      appOpened = true;
    };

    window.addEventListener("pagehide", markAppOpened, { once: true });
    document.addEventListener("visibilitychange", markAppOpened, { once: true });

    window.location.href = targetUrl;

    window.setTimeout(() => {
      if (!appOpened) {
        window.location.href = webUrl;
      }
    }, 900);
  }
};
