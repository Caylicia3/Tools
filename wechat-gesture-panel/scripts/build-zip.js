const fs = require('fs');
const path = require('path');
const { spawn } = require('child_process');

const cwd = process.cwd();
const extDir = path.join(cwd, 'extension');
const outZip = path.join(cwd, 'wechat-gesture-panel-extension.zip');
const sevenZip = process.platform === 'win32' ? '7z.exe' : '7z';

function main() {
  if (!fs.existsSync(extDir)) {
    console.error('未找到 extension/ 目录，请确认在项目根目录运行。');
    process.exit(1);
  }

  if (fs.existsSync(outZip)) {
    fs.unlinkSync(outZip);
  }

  const args = ['a', '-tzip', outZip, path.join(extDir, '*')];
  const proc = spawn(sevenZip, args, { shell: false, stdio: 'pipe' });

  proc.on('error', (err) => {
    console.log('未检测到 7z/7z.exe，请手动将 extension/ 目录打包为 wechat-gesture-panel-extension.zip。');
    console.error(err.message);
    process.exit(0);
  });

  proc.on('close', (code) => {
    if (code === 0) {
      console.log('已生成：', outZip);
    } else {
      console.log('打包结束，退出码：', code);
    }
  });
}

main();
