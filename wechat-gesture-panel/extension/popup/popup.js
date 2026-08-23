document.getElementById('toggleBtn').addEventListener('click', async () => {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab) return;
  chrome.tabs.sendMessage(tab.id, { type: 'TOGGLE_PANEL' }, (response) => {
    if (chrome.runtime.lastError) {
      updateStatus('未在微信页面运行');
    }
  });
});

document.getElementById('optionsBtn').addEventListener('click', () => {
  chrome.runtime.openOptionsPage();
});

function updateStatus(text) {
  document.getElementById('status').textContent = '状态：' + text;
}

async function refreshStatus() {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab) {
    updateStatus('未找到标签页');
    return;
  }
  chrome.tabs.sendMessage(tab.id, { type: 'GET_STATUS' }, (response) => {
    if (chrome.runtime.lastError || !response) {
      updateStatus('未在微信页面运行');
      return;
    }
    updateStatus(response.active ? (response.cameraActive ? '运行中 · 摄像头已开启' : '运行中 · 摄像头未开启') : '已暂停');
  });
}

refreshStatus();
