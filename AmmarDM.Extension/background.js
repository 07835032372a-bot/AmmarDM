// Ammar DM Background Service Worker - محسّن مع كاش ذكي وتنزيل آني
const NATIVE_HOST = "com.ammardm.downloader";
const LOCAL_API = "http://127.0.0.1:57891/download";
const SETTINGS_API = "http://127.0.0.1:57891/settings";
const EXTRACT_API = "http://127.0.0.1:57891/extract_media";
const DOWNLOAD_API = "http://127.0.0.1:57891/download_youtube";

let cachedSettings = {
  extensions: ["ZIP","RAR","7Z","EXE","ISO","MSI","TAR","GZ","MP4","MKV","WEBM","FLV","AVI","MOV","MP3","WAV","AAC","PDF","DOCX","XLSX"],
  ignoredSites: ["update.microsoft.com","windowsupdate.microsoft.com"],
  captureDownloads: true
};

// كاش صيغ اليوتيوب حتى لا يُعاد الاستخراج كل مرة (3 دقائق كحد أقصى)
const formatCache = {};
const CACHE_TTL = 3 * 60 * 1000; // 3 دقائق

function refreshSettings() {
  fetch(SETTINGS_API)
    .then(r => r.json())
    .then(data => { cachedSettings = data; })
    .catch(() => {});
}
refreshSettings();
setInterval(refreshSettings, 30000);

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.action === "get_settings") {
    fetch(SETTINGS_API)
      .then(r => r.json())
      .then(data => { try { sendResponse({ success: true, data: data }); } catch {} })
      .catch(err => { try { sendResponse({ success: false, error: err.message }); } catch {} });
    return true;
  }

  if (message.action === "extract_youtube") {
    const url = message.url;
    const now = Date.now();

    // تحقق من الكاش: إذا كانت الصيغ محفوظة وحديثة (أقل من 3 دقائق)
    if (formatCache[url] && (now - formatCache[url].timestamp < CACHE_TTL)) {
      try { sendResponse({ success: true, data: formatCache[url].data }); } catch {}
      return true;
    }

    // استخراج جديد من السيرفر
    fetch(EXTRACT_API, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ url: url })
    })
    .then(r => r.json())
    .then(data => {
      if (data && data.formats && data.formats.length > 0) {
        formatCache[url] = { timestamp: Date.now(), data: data };
      }
      try { sendResponse({ success: true, data: data }); } catch {}
    })
    .catch(err => { try { sendResponse({ success: false, error: err.message }); } catch {} });
    return true;
  }

  // تنزيل فيديو يوتيوب بالصيغة المختارة - يُعيد توليد الرابط المباشر لحظياً
  if (message.action === "download_youtube_format") {
    const { pageUrl, formatIndex, fileName } = message;
    // إرسال مباشر للبرنامج: سيقوم البرنامج باستخراج الرابط الحي وتحميله
    fetch(DOWNLOAD_API, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ PageUrl: pageUrl, FormatIndex: formatIndex, FileName: fileName })
    })
    .then(r => r.json())
    .then(() => { try { sendResponse({ success: true }); } catch {} })
    .catch(() => {
      // fallback: أرسل الرابط الموجود في الكاش مباشرة
      const cached = formatCache[pageUrl];
      if (cached && cached.data && cached.data.formats && cached.data.formats[formatIndex]) {
        const f = cached.data.formats[formatIndex];
        sendDownloadToApp({
          Action: "add_download",
          Url: f.DirectUrl,
          FileName: fileName,
          Referrer: pageUrl,
          TotalBytes: f.SizeBytes || 0
        });
      }
      try { sendResponse({ success: false }); } catch {}
    });
    return true;
  }

  if (message.action === "trigger_download") {
    sendDownloadToApp(message.payload);
    try { sendResponse({ success: true }); } catch {}
    return true;
  }
});

// مراقبة واقتناص التنزيلات العادية
chrome.downloads.onCreated.addListener((downloadItem) => {
  if (!cachedSettings.captureDownloads) return;

  const url = (downloadItem.url || "").toLowerCase();
  const filename = (downloadItem.filename || "").toLowerCase();

  if (cachedSettings.ignoredSites && cachedSettings.ignoredSites.some(site => url.includes(site.toLowerCase()))) {
    return;
  }

  const isTargetExtension = cachedSettings.extensions.some(ext => {
    const extWithDot = "." + ext.toLowerCase();
    return filename.endsWith(extWithDot) || url.includes(extWithDot);
  });

  if (isTargetExtension && (url.startsWith("http://") || url.startsWith("https://"))) {
    chrome.downloads.cancel(downloadItem.id, () => {
      chrome.downloads.erase({ id: downloadItem.id });
      sendDownloadToApp({
        Action: "add_download",
        Url: downloadItem.url,
        FileName: downloadItem.filename || "",
        Referrer: downloadItem.referrer || "",
        TotalBytes: downloadItem.fileSize || 0
      });
    });
  }
});

function sendDownloadToApp(data) {
  fetch(LOCAL_API, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data)
  })
  .then(() => {})
  .catch(() => {
    try {
      const port = chrome.runtime.connectNative(NATIVE_HOST);
      port.postMessage(data);
    } catch {}
  });
}
