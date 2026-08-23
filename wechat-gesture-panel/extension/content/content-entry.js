(function () {
  'use strict';

  const HOST_ID = 'wechat-gesture-panel-host';

  let host = null;
  let shadow = null;
  let panelApi = null;
  let cameraApi = null;
  let engineApi = null;
  let video = null;
  let currentSensitivity = 0.5;
  let mounted = false;

  function getHost() {
    return document.getElementById(HOST_ID);
  }

  async function loadText(url) {
    const res = await fetch(url);
    if (!res.ok) throw new Error(`Failed to load ${url}: ${res.status}`);
    return res.text();
  }

  async function mount() {
    if (mounted && host && host.isConnected) return;
    const existing = getHost();
    if (existing) existing.remove();

    host = document.createElement('div');
    host.id = HOST_ID;
    host.style.cssText = 'position:fixed;inset:0;pointer-events:none;z-index:2147483647;';
    document.body.appendChild(host);
    shadow = host.attachShadow({ mode: 'open' });

    const [panelHtml, cssUrl] = await Promise.all([
      loadText(chrome.runtime.getURL('content/gesture-panel.html')),
      Promise.resolve(chrome.runtime.getURL('content/gesture-panel.css'))
    ]);

    const styleLink = document.createElement('link');
    styleLink.rel = 'stylesheet';
    styleLink.href = cssUrl;
    shadow.appendChild(styleLink);

    const root = document.createElement('div');
    root.innerHTML = panelHtml;
    shadow.appendChild(root);

    const { initGesturePanel } = await import(chrome.runtime.getURL('content/gesture-panel.js'));
    panelApi = initGesturePanel(shadow, {
      onAction: handlePanelAction
    });

    mounted = true;
    window.__WECHAT_GESTURE_PANEL_HOST__ = host;

    await initCameraAndEngine();
  }

  async function initCameraAndEngine() {
    video = document.createElement('video');
    video.setAttribute('autoplay', '');
    video.setAttribute('playsinline', '');
    video.setAttribute('muted', '');
    video.style.cssText = 'position:fixed;left:-9999px;width:1px;height:1px;opacity:0;pointer-events:none;';
    document.body.appendChild(video);

    const [{ initCamera }, { initGestureEngine }] = await Promise.all([
      import(chrome.runtime.getURL('content/camera-bridge.js')),
      import(chrome.runtime.getURL('content/gesture-engine.js'))
    ]);

    cameraApi = initCamera({
      videoElement: video,
      onError: (error) => {
        console.error('[WeChatGesturePanel] camera error', error);
        panelApi?.setCameraStatus(false, '摄像头 · 未授权');
      }
    });

    engineApi = initGestureEngine({
      video,
      sensitivity: currentSensitivity,
      onGesture: handleGesture
    });

    try {
      await cameraApi.start();
      panelApi?.setCameraStatus(true, '摄像头 · 开启');
      await engineApi.start();
    } catch (error) {
      panelApi?.setCameraStatus(false, '摄像头 · 未授权');
    }
  }

  function handleGesture(action) {
    executeAction(action);
  }

  function handlePanelAction(action) {
    executeAction(action);
  }

  const actionLabels = {
    'scroll-up': '上滑',
    'scroll-down': '下滑',
    'select': '选中',
    'open': '打开'
  };

  function executeAction(action) {
    switch (action) {
      case 'scroll-up':
        window.scrollBy({ top: -320, behavior: 'smooth' });
        break;
      case 'scroll-down':
        window.scrollBy({ top: 320, behavior: 'smooth' });
        break;
      case 'open': {
        const el = document.elementFromPoint(window.innerWidth / 2, window.innerHeight / 2);
        if (el) {
          el.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, view: window }));
        }
        break;
      }
      case 'select': {
        const active = document.activeElement && document.activeElement !== document.body
          ? document.activeElement
          : document.querySelector('a, button, input, textarea, [tabindex]:not([tabindex="-1"])');
        if (active) {
          active.focus({ preventScroll: true });
          active.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, view: window }));
        }
        break;
      }
      case 'toggle-panel':
        panelApi?.toggle();
        break;
    }

    if (panelApi && action !== 'toggle-panel') {
      panelApi.setCurrentGesture(actionLabels[action] || action);
      panelApi.setActiveAction(action);
    }
  }

  function teardown() {
    engineApi?.stop();
    cameraApi?.stop();
    panelApi?.destroy();
    if (video && video.parentNode) video.parentNode.removeChild(video);
    if (host && host.parentNode) host.parentNode.removeChild(host);
    host = null;
    shadow = null;
    panelApi = null;
    cameraApi = null;
    engineApi = null;
    video = null;
    mounted = false;
    delete window.__WECHAT_GESTURE_PANEL_HOST__;
  }

  async function applySettings(settings) {
    const enabled = settings.enabled !== false;
    if (typeof settings.sensitivity === 'number') {
      currentSensitivity = settings.sensitivity;
    }

    if (enabled) {
      if (!mounted) {
        await mount();
      } else {
        panelApi?.show();
        if (cameraApi && !cameraApi.isActive()) {
          cameraApi.start().then(() => engineApi?.start()).catch(() => {});
        }
      }
    } else {
      if (mounted) {
        panelApi?.hide();
        engineApi?.stop();
        cameraApi?.stop();
      }
    }
  }

  chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (!message || !message.type) return false;

    if (message.type === 'TOGGLE_PANEL') {
      (async () => {
        if (!mounted) await mount();
        panelApi?.toggle();
        sendResponse({ ok: true });
      })();
      return true;
    }

    if (message.type === 'GET_STATUS') {
      sendResponse({
        active: mounted && !!host && host.isConnected,
        cameraActive: cameraApi ? cameraApi.isActive() : false,
        enabled: true
      });
      return false;
    }

    if (message.type === 'UPDATE_SETTINGS') {
      applySettings({
        enabled: message.enabled,
        sensitivity: message.sensitivity
      });
      sendResponse({ ok: true });
      return false;
    }

    return false;
  });

  chrome.storage.sync.get(['enabled', 'sensitivity'], (result) => {
    if (chrome.runtime.lastError) {
      console.error('[WeChatGesturePanel] storage error', chrome.runtime.lastError);
      return;
    }
    applySettings({
      enabled: result.enabled,
      sensitivity: result.sensitivity
    });
  });
})();
