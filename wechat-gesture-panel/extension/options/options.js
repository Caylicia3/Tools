const elSensitivity = document.getElementById('sensitivity');
const elEnabled = document.getElementById('enabled');
const btnSave = document.getElementById('save');
const elSaved = document.getElementById('saved');

chrome.storage.sync.get(['sensitivity', 'enabled'], (result) => {
  elSensitivity.value = typeof result.sensitivity === 'number' ? result.sensitivity : 0.5;
  elEnabled.checked = result.enabled !== false;
});

btnSave.addEventListener('click', () => {
  const sensitivity = parseFloat(elSensitivity.value);
  const enabled = elEnabled.checked;
  chrome.storage.sync.set({ sensitivity, enabled }, () => {
    elSaved.classList.add('is-visible');
    setTimeout(() => elSaved.classList.remove('is-visible'), 1500);
    chrome.tabs.query({ url: ['https://www.bilibili.com/*'] }, (tabs) => {
      tabs.forEach((tab) => {
        chrome.tabs.sendMessage(tab.id, { type: 'UPDATE_SETTINGS', sensitivity, enabled }).catch(() => {});
      });
    });
  });
});
