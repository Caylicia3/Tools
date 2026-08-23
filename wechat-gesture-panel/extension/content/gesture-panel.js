const ONBOARDING_KEY = 'gesture-onboarding-shown';

export function initGesturePanel(shadowRoot, options = {}) {
  const panel = shadowRoot.getElementById('gesturePanel');
  const collapsedBar = shadowRoot.getElementById('collapsedBar');
  const expandToggle = shadowRoot.getElementById('expandToggle');
  const collapseToggle = shadowRoot.getElementById('collapseToggle');
  const exitBtns = [shadowRoot.getElementById('exitPanel'), shadowRoot.getElementById('exitPanelExpanded')];
  const exitToast = shadowRoot.getElementById('exitToast');
  const restoreBtn = shadowRoot.getElementById('restorePanel');
  const replayBtn = shadowRoot.getElementById('replayOnboarding');
  const cameraLabel = shadowRoot.getElementById('cameraLabel');
  const cameraDot = shadowRoot.getElementById('cameraDot');
  const gestureValue = shadowRoot.getElementById('gestureValue');
  const overlay = shadowRoot.getElementById('onboardingOverlay');
  const ring = shadowRoot.getElementById('spotlightRing');
  const titleEl = shadowRoot.getElementById('onboardingTitle');
  const textEl = shadowRoot.getElementById('onboardingText');
  const nextBtn = shadowRoot.getElementById('onboardingNext');
  const skipBtn = shadowRoot.getElementById('onboardingSkip');

  let isDragging = false;
  let startX = 0;
  let startY = 0;
  let baseX = 0;
  let baseY = 0;
  let toastTimer = null;
  let stepIndex = 0;
  let destroyed = false;

  const steps = [
    {
      target: '.drag-handle',
      title: '拖动面板',
      text: '按住把手拖动面板，调整到你顺手的位置。'
    },
    {
      target: '#gestureHelp',
      title: '手势说明',
      text: '上下挥动=滚动，握拳=选中，张开手=打开。'
    },
    {
      target: '#actionChips',
      title: '自动执行',
      text: '识别到对应手势后，面板会自动执行操作。',
      action: '知道了'
    }
  ];

  function isControl(target) {
    return target.closest('.panel-toggle, .panel-exit, .toast-restore, .replay-btn, .onboarding-btn, .chip');
  }

  function clamp(val, min, max) {
    return Math.max(min, Math.min(max, val));
  }

  function getBounds() {
    const rect = panel.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;
    return {
      minX: -(vw - rect.width - 16),
      maxX: 16,
      minY: -16,
      maxY: vh - rect.height - 16
    };
  }

  function setTranslate(x, y, scale) {
    const s = scale ? ' scale(1.02)' : '';
    panel.style.transform = `translate3d(${x.toFixed(2)}px, ${y.toFixed(2)}px, 0)${s}`;
  }

  function expandPanel() {
    panel.classList.remove('collapsed');
    panel.classList.add('expanded');
    panel.setAttribute('aria-expanded', 'true');
  }

  function collapsePanel() {
    panel.classList.remove('expanded');
    panel.classList.add('collapsed');
    panel.setAttribute('aria-expanded', 'false');
  }

  function showPanel() {
    panel.classList.remove('is-hidden');
    panel.setAttribute('aria-hidden', 'false');
  }

  function hidePanel() {
    panel.classList.add('is-hidden');
    panel.setAttribute('aria-hidden', 'true');
  }

  function showToast() {
    exitToast.classList.add('is-visible');
    exitToast.setAttribute('aria-hidden', 'false');
  }

  function hideToast() {
    exitToast.classList.remove('is-visible');
    exitToast.setAttribute('aria-hidden', 'true');
  }

  function exitPanel() {
    hidePanel();
    showToast();
    if (toastTimer) clearTimeout(toastTimer);
    toastTimer = setTimeout(() => {
      hideToast();
      showPanel();
    }, 5000);
  }

  function restorePanel() {
    if (toastTimer) clearTimeout(toastTimer);
    hideToast();
    showPanel();
  }

  function togglePanel() {
    if (panel.classList.contains('expanded')) {
      collapsePanel();
    } else {
      expandPanel();
    }
  }

  function onPointerDown(e) {
    if (isControl(e.target)) return;
    isDragging = true;
    panel.classList.add('is-dragging');
    panel.setPointerCapture(e.pointerId);
    startX = e.clientX;
    startY = e.clientY;
    setTranslate(baseX, baseY, true);
  }

  function onPointerMove(e) {
    if (!isDragging) return;
    e.preventDefault();
    const dx = e.clientX - startX;
    const dy = e.clientY - startY;
    const bounds = getBounds();
    const x = clamp(baseX + dx, bounds.minX, bounds.maxX);
    const y = clamp(baseY + dy, bounds.minY, bounds.maxY);
    setTranslate(x, y, true);
  }

  function onPointerUp(e) {
    if (!isDragging) return;
    isDragging = false;
    panel.classList.remove('is-dragging');
    panel.releasePointerCapture(e.pointerId);
    const dx = e.clientX - startX;
    const dy = e.clientY - startY;
    const bounds = getBounds();
    baseX = clamp(baseX + dx, bounds.minX, bounds.maxX);
    baseY = clamp(baseY + dy, bounds.minY, bounds.maxY);
    setTranslate(baseX, baseY, false);
  }

  function onPointerCancel() {
    if (!isDragging) return;
    isDragging = false;
    panel.classList.remove('is-dragging');
    setTranslate(baseX, baseY, false);
  }

  function setCameraStatus(active, label) {
    if (cameraLabel) cameraLabel.textContent = label || (active ? '摄像头 · 开启' : '摄像头 · 关闭');
    if (cameraDot) cameraDot.classList.toggle('is-off', !active);
  }

  function setCurrentGesture(name) {
    if (gestureValue) gestureValue.textContent = name || '-';
  }

  function setActiveAction(action) {
    const chips = shadowRoot.querySelectorAll('.chip[data-action]');
    const map = {
      'scroll-up': 'scroll',
      'scroll-down': 'scroll',
      'open': 'open',
      'select': 'select'
    };
    const target = map[action];
    chips.forEach((chip) => {
      const isMatch = chip.dataset.action === target;
      chip.classList.toggle('chip-active', isMatch);
      chip.classList.toggle('chip-ghost', !isMatch);
    });
  }

  function positionSpotlight(selector) {
    const target = shadowRoot.querySelector(selector);
    if (!target) return;
    const rect = target.getBoundingClientRect();
    ring.style.width = (rect.width + 8) + 'px';
    ring.style.height = (rect.height + 8) + 'px';
    ring.style.left = (rect.left - 4) + 'px';
    ring.style.top = (rect.top - 4) + 'px';
    ring.classList.add('is-active');
  }

  function renderStep(i) {
    const step = steps[i];
    if (titleEl) titleEl.textContent = step.title;
    if (textEl) textEl.textContent = step.text;
    if (nextBtn) nextBtn.textContent = step.action || '下一步';
    positionSpotlight(step.target);
  }

  function startOnboarding() {
    expandPanel();
    stepIndex = 0;
    overlay.classList.add('is-active');
    overlay.setAttribute('aria-hidden', 'false');
    renderStep(stepIndex);
  }

  function finishOnboarding() {
    overlay.classList.remove('is-active');
    overlay.setAttribute('aria-hidden', 'true');
    ring.classList.remove('is-active');
    try {
      localStorage.setItem(ONBOARDING_KEY, '1');
    } catch (_) {}
  }

  function maybeStartOnboarding() {
    try {
      if (localStorage.getItem(ONBOARDING_KEY) === '1') return;
    } catch (_) {}
    requestAnimationFrame(startOnboarding);
  }

  function bindEvents() {
    panel.addEventListener('pointerdown', onPointerDown);
    panel.addEventListener('pointermove', onPointerMove);
    panel.addEventListener('pointerup', onPointerUp);
    panel.addEventListener('pointercancel', onPointerCancel);

    collapsedBar.addEventListener('click', (e) => {
      if (isControl(e.target)) return;
      expandPanel();
    });

    expandToggle.addEventListener('click', (e) => {
      e.stopPropagation();
      expandPanel();
    });

    collapseToggle.addEventListener('click', (e) => {
      e.stopPropagation();
      collapsePanel();
    });

    exitBtns.forEach((btn) => {
      if (!btn) return;
      btn.addEventListener('click', (e) => {
        e.stopPropagation();
        exitPanel();
      });
    });

    restoreBtn.addEventListener('click', restorePanel);
    replayBtn.addEventListener('click', startOnboarding);

    nextBtn.addEventListener('click', () => {
      if (stepIndex < steps.length - 1) {
        stepIndex++;
        renderStep(stepIndex);
      } else {
        finishOnboarding();
      }
    });

    skipBtn.addEventListener('click', finishOnboarding);

    window.addEventListener('resize', () => {
      if (isDragging) return;
      const bounds = getBounds();
      baseX = clamp(baseX, bounds.minX, bounds.maxX);
      baseY = clamp(baseY, bounds.minY, bounds.maxY);
      setTranslate(baseX, baseY, false);
    });
  }

  function destroy() {
    if (destroyed) return;
    destroyed = true;
    if (toastTimer) clearTimeout(toastTimer);
    panel.removeEventListener('pointerdown', onPointerDown);
    panel.removeEventListener('pointermove', onPointerMove);
    panel.removeEventListener('pointerup', onPointerUp);
    panel.removeEventListener('pointercancel', onPointerCancel);
    hidePanel();
  }

  bindEvents();
  maybeStartOnboarding();

  return {
    expand: expandPanel,
    collapse: collapsePanel,
    toggle: togglePanel,
    show: showPanel,
    hide: hidePanel,
    setCameraStatus,
    setCurrentGesture,
    setActiveAction,
    startOnboarding,
    destroy
  };
}
