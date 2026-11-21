// Ammar DM Video Sniffer & Multi-Format Floating Panel (IDM Style) - محسّن
(function() {
  'use strict';

  let currentSettings = {
    showVideoPanel: true,
    ignoredSites: []
  };

  const hostname = window.location.hostname.toLowerCase();
  const isYouTube = hostname.includes("youtube.com") || hostname.includes("youtu.be");

  try {
    chrome.runtime.sendMessage({ action: "get_settings" }, res => {
      if (chrome.runtime.lastError) return;
      if (res && res.success && res.data) {
        currentSettings = res.data;
      }
    });
  } catch {}

  function hookVideo(video) {
    if (video.dataset.ammardmHooked) return;
    video.dataset.ammardmHooked = "true";

    if (currentSettings.ignoredSites && currentSettings.ignoredSites.some(site => hostname.includes(site.toLowerCase()))) {
      return;
    }

    const panelContainer = document.createElement("div");
    panelContainer.className = "ammardm-video-container";

    const btn = document.createElement("div");
    btn.className = "ammardm-video-panel";
    btn.innerHTML = `<span class="ammardm-video-panel-icon">📥</span><span>تحميل هذا الفيديو ▼</span>`;

    const dropdown = document.createElement("div");
    dropdown.className = "ammardm-dropdown";

    // كاش الصيغ المحلي لهذا الفيديو
    let cachedFormats = null;
    let cachedTitle = null;
    const pageUrl = window.location.href;

    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      e.preventDefault();

      if (dropdown.style.display === "block") {
        dropdown.style.display = "none";
        return;
      }

      dropdown.style.display = "block";

      // إذا كان الكاش المحلي متاحاً اعرضه فوراً بدون انتظار
      if (cachedFormats && cachedFormats.length > 0) {
        renderYouTubeFormats(cachedTitle, cachedFormats, dropdown, pageUrl);
        return;
      }

      dropdown.innerHTML = `<div style="padding:10px;font-size:11px;color:#666;text-align:center;">⏳ جاري استخراج الصيغ...</div>`;

      if (isYouTube) {
        chrome.runtime.sendMessage({
          action: "extract_youtube",
          url: pageUrl
        }, response => {
          if (chrome.runtime.lastError) {
            renderFallbackYouTubeFormats(dropdown, pageUrl);
            return;
          }
          if (response && response.success && response.data && response.data.formats && response.data.formats.length > 0) {
            cachedFormats = response.data.formats;
            cachedTitle = response.data.title;
            renderYouTubeFormats(response.data.title, response.data.formats, dropdown, pageUrl);
          } else {
            renderFallbackYouTubeFormats(dropdown, pageUrl);
          }
        });
      } else {
        populateGenericFormats(video, dropdown);
      }
    });

    panelContainer.appendChild(btn);
    panelContainer.appendChild(dropdown);
    document.body.appendChild(panelContainer);

    document.addEventListener("click", () => {
      dropdown.style.display = "none";
    });

    function updatePosition() {
      const rect = video.getBoundingClientRect();
      if (rect.width > 140 && rect.height > 90 && rect.bottom > 0 && rect.top < window.innerHeight) {
        panelContainer.style.display = "block";
        panelContainer.style.top = `${window.scrollY + rect.top + 10}px`;
        panelContainer.style.left = `${window.scrollX + rect.right - 160}px`;
      } else {
        panelContainer.style.display = "none";
      }
    }

    window.addEventListener("scroll", updatePosition, { passive: true });
    window.addEventListener("resize", updatePosition, { passive: true });
    setInterval(updatePosition, 1000);
    updatePosition();
  }

  function renderYouTubeFormats(videoTitle, formats, dropdown, pageUrl) {
    dropdown.innerHTML = "";
    formats.forEach((f, idx) => {
      const item = document.createElement("div");
      item.className = "ammardm-dropdown-item";
      item.innerHTML = `<span>${f.DisplayLabel}</span><span style="color:#0078D7;font-weight:bold;margin-right:8px;">تحميل</span>`;
      item.addEventListener("click", (e) => {
        e.stopPropagation();
        dropdown.style.display = "none";

        const cleanTitle = (videoTitle || "YouTube_Video").replace(/[\\/:*?"<>|]/g, "_").trim().substring(0, 60);
        const fileName = `${cleanTitle}.${f.Container}`;

        // أرسل طلب التنزيل بالصيغة المختارة (سيُعيد البرنامج توليد الرابط المباشر الحي)
        // هذا يحل مشكلة انتهاء صلاحية رابط googlevideo.com
        try {
          chrome.runtime.sendMessage({
            action: "download_youtube_format",
            pageUrl: pageUrl,
            formatIndex: idx,
            fileName: fileName
          }, () => { if (chrome.runtime.lastError) {} });
        } catch {}
      });
      dropdown.appendChild(item);
    });
  }

  function renderFallbackYouTubeFormats(dropdown, pageUrl) {
    dropdown.innerHTML = "";
    const title = (document.title || "YouTube_Video").replace(/[\\/:*?"<>|]/g, "_").trim().substring(0, 60);

    const fallbacks = [
      { label: "🎬 فيديو MP4 (1080p)", ext: "mp4" },
      { label: "🎬 فيديو MP4 (720p)", ext: "mp4" },
      { label: "🎵 صوت فقط (MP3)", ext: "mp3" }
    ];

    fallbacks.forEach(f => {
      const item = document.createElement("div");
      item.className = "ammardm-dropdown-item";
      item.innerHTML = `<span>${f.label}</span><span style="color:#0078D7;font-weight:bold;margin-right:8px;">تحميل</span>`;
      item.addEventListener("click", (e) => {
        e.stopPropagation();
        dropdown.style.display = "none";
        triggerDownload(pageUrl, `${title}.${f.ext}`, 0);
      });
      dropdown.appendChild(item);
    });
  }

  function populateGenericFormats(video, dropdown) {
    dropdown.innerHTML = "";
    const formats = [];
    const directSrc = video.currentSrc || video.src;
    if (directSrc && !directSrc.startsWith("blob:")) {
      formats.push({ label: "🎬 فيديو MP4 (أصلي)", url: directSrc, ext: "mp4" });
    }

    const sources = video.querySelectorAll("source");
    sources.forEach((s, idx) => {
      if (s.src && !s.src.startsWith("blob:")) {
        const quality = s.getAttribute("size") || s.getAttribute("res") || (idx === 0 ? "HD" : "SD");
        formats.push({ label: `🎬 فيديو MP4 (${quality})`, url: s.src, ext: "mp4" });
      }
    });

    formats.push({ label: "🎵 صوت MP3 (Audio)", url: directSrc || window.location.href, ext: "mp3" });

    formats.forEach(f => {
      const item = document.createElement("div");
      item.className = "ammardm-dropdown-item";
      item.innerHTML = `<span>${f.label}</span><span style="color:#0078D7;font-weight:bold;">تحميل</span>`;
      item.addEventListener("click", (e) => {
        e.stopPropagation();
        dropdown.style.display = "none";
        const title = (document.title || "video").replace(/[\\/:*?"<>|]/g, "_").trim().substring(0, 60);
        triggerDownload(f.url, `${title}.${f.ext}`, 0);
      });
      dropdown.appendChild(item);
    });
  }

  function triggerDownload(url, fileName, sizeBytes) {
    try {
      chrome.runtime.sendMessage({
        action: "trigger_download",
        payload: {
          Action: "add_download",
          Url: url,
          FileName: fileName,
          Referrer: window.location.href,
          TotalBytes: sizeBytes || 0
        }
      }, () => { if (chrome.runtime.lastError) {} });
    } catch {}
  }

  function scan() {
    document.querySelectorAll("video").forEach(hookVideo);
  }

  const observer = new MutationObserver(scan);
  observer.observe(document.documentElement, { childList: true, subtree: true });
  window.addEventListener("load", scan);
  scan();
})();
