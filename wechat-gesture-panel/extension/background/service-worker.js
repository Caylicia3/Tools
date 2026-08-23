chrome.runtime.onInstalled.addListener((details) => {
  if (details.reason === 'install') {
    chrome.storage.sync.set({
      enabled: true,
      sensitivity: 0.5,
      showOnboarding: true,
      panelPosition: { x: 0, y: 0, expanded: false }
    });
  }
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message && message.target === 'background') {
    chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
      if (tabs && tabs[0]) {
        chrome.tabs.sendMessage(tabs[0].id, message, (response) => {
          if (chrome.runtime.lastError) {
            sendResponse({ ok: false, error: chrome.runtime.lastError.message });
          } else {
            sendResponse(response || { ok: true });
          }
        });
      } else {
        sendResponse({ ok: false, error: 'no active tab' });
      }
    });
    return true;
  }
  sendResponse({ ok: false, error: 'unknown target' });
  return false;
});
