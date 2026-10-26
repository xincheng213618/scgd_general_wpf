const shapeNames = {
  rectangle: '规则矩形',
  pincushion: '向内弯曲（枕形）',
  barrel: '向外弯曲（桶形）',
  keystone: '上下边长不等（梯形）',
  rotation: '整体旋转',
};

function validateDimensions(rows, cols) {
  if (![rows, cols].every(value => Number.isInteger(value) && value >= 3 && value <= 15 && value % 2 === 1)) {
    throw new RangeError('点阵行列数必须为 3 到 15 的奇数。');
  }
}

/** Build synthetic measured coordinates, without invoking image detection or optical fitting. */
export function createGrid(rows, cols, shape, amount) {
  validateDimensions(rows, cols);
  if (!Object.hasOwn(shapeNames, shape)) throw new RangeError('未知示意形状。');
  if (!Number.isFinite(amount) || amount < 0 || amount > 20) throw new RangeError('示意幅度须在 0 到 20 之间。');
  const points = [];
  const a = amount / 100;
  const radians = amount * Math.PI / 180;
  for (let row = 0; row < rows; row++) {
    for (let col = 0; col < cols; col++) {
      let x = (2 * col / (cols - 1) - 1) * 600;
      let y = (2 * row / (rows - 1) - 1) * 400;
      if (shape === 'pincushion' || shape === 'barrel') {
        const factor = 1 + (shape === 'barrel' ? -1 : 1) * a * (x * x + y * y) / 520000;
        x *= factor;
        y *= factor;
      } else if (shape === 'keystone') {
        x *= 1 + a * y / 400;
      } else if (shape === 'rotation') {
        [x, y] = [x * Math.cos(radians) - y * Math.sin(radians), x * Math.sin(radians) + y * Math.cos(radians)];
      }
      points.push({ id: row * cols + col, row, col, x, y });
    }
  }
  return points;
}

const distance = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
const signedDistance = (point, start, end) => ((end.x - start.x) * (point.y - start.y) - (end.y - start.y) * (point.x - start.x)) / distance(start, end);
const horizontalTilt = (a, b) => Math.atan2(Math.abs(b.y - a.y), Math.abs(b.x - a.x)) * 180 / Math.PI;
const verticalTilt = (a, b) => Math.atan2(Math.abs(b.x - a.x), Math.abs(b.y - a.y)) * 180 / Math.PI;

/** Geometry formulas mirror GridDistortionAnalysis; all percentages are already multiplied by 100. */
export function analyzeGrid(points, rows, cols) {
  validateDimensions(rows, cols);
  if (!Array.isArray(points) || points.length !== rows * cols) throw new RangeError('需要完整点阵。');
  const grid = new Map();
  const ids = new Set();
  for (const point of points) {
    const key = `${point.row},${point.col}`;
    if (!Number.isInteger(point.row) || !Number.isInteger(point.col) || point.row < 0 || point.row >= rows || point.col < 0 || point.col >= cols
      || !Number.isFinite(point.x) || !Number.isFinite(point.y) || grid.has(key) || ids.has(point.id)) throw new RangeError('点阵坐标无效或存在重复点。');
    grid.set(key, point);
    ids.add(point.id);
  }
  const r = [0, (rows - 1) / 2, rows - 1];
  const c = [0, (cols - 1) / 2, cols - 1];
  const referencePoints = r.flatMap(row => c.map(col => grid.get(`${row},${col}`))).map((point, index) => ({ ...point, name: String.fromCharCode(65 + index) }));
  const [tl, tc, tr, ml, , mr, bl, bc, br] = referencePoints;
  const topWidth = distance(tl, tr), middleWidth = distance(ml, mr), bottomWidth = distance(bl, br);
  const leftHeight = distance(tl, bl), centerHeight = distance(tc, bc), rightHeight = distance(tr, br);
  const widthMean = (topWidth + bottomWidth) / 2, heightMean = (leftHeight + rightHeight) / 2;
  if (![topWidth, middleWidth, bottomWidth, leftHeight, centerHeight, rightHeight].every(value => Number.isFinite(value) && value > 1e-9)) throw new RangeError('点阵跨度退化。');
  const cross = (mr.x - ml.x) * (bc.y - tc.y) - (mr.y - ml.y) * (bc.x - tc.x);
  if (!Number.isFinite(cross) || Math.abs(cross) <= 1e-6 * middleWidth * centerHeight) throw new RangeError('点阵横纵方向退化为共线。');
  const keystoneHorizontalPercent = 100 * (leftHeight - rightHeight) / heightMean;
  const keystoneVerticalPercent = 100 * (topWidth - bottomWidth) / widthMean;
  const topTiltDegrees = horizontalTilt(tl, tr), bottomTiltDegrees = horizontalTilt(bl, br);
  const leftTiltDegrees = verticalTilt(tl, bl), rightTiltDegrees = verticalTilt(tr, br);
  return {
    horizontalTvPercent: 100 * (widthMean - middleWidth) / middleWidth,
    verticalTvPercent: 100 * (heightMean - centerHeight) / centerHeight,
    topPercent: 100 * signedDistance(tc, tl, tr) / heightMean,
    bottomPercent: -100 * signedDistance(bc, bl, br) / heightMean,
    leftPercent: -100 * signedDistance(ml, tl, bl) / widthMean,
    rightPercent: 100 * signedDistance(mr, tr, br) / widthMean,
    keystoneHorizontalPercent,
    keystoneVerticalPercent,
    topTiltDegrees, bottomTiltDegrees, leftTiltDegrees, rightTiltDegrees,
    maximumTiltDegrees: Math.max(topTiltDegrees, bottomTiltDegrees, leftTiltDegrees, rightTiltDegrees),
    maximumEdgeLengthDifferencePercent: Math.max(Math.abs(keystoneHorizontalPercent), Math.abs(keystoneVerticalPercent)),
    topWidth, middleWidth, bottomWidth, leftHeight, centerHeight, rightHeight,
    referencePoints,
  };
}

const explanations = {
  rectangle: '规则矩形的 TV、四边和梯形指标均为零，四边与图像坐标方向对齐。',
  pincushion: '四边向内弯曲，TV 和四边畸变为正；四角仍构成等对边矩形，最大边长差可为零。',
  barrel: '四边向外弯曲，TV 和四边畸变为负；四角仍构成等对边矩形，最大边长差可为零。',
  keystone: '上、下宽度不同，体现在 Keystone Vertical 和最大边长差中；中间宽度仍等于上下均值，水平 TV 可为零。',
  rotation: '整体旋转保持长度和相对几何关系，TV、四边和梯形可保持为零，最大倾斜角随旋转角变化。',
};

let nextInstance = 0;
const mounts = new WeakMap();

/** Shared browser implementation for both the VitePress component and the packaged offline article. */
export function mountGridDistortionDemo(element) {
  if (!element || typeof element.querySelector !== 'function') throw new TypeError('需要示意图的容器元素。');
  mounts.get(element)?.();
  const document = element.ownerDocument;
  const window = document.defaultView;
  const id = `cv-grid-demo-${++nextInstance}`;
  element.classList.add('cv-grid-demo');
  element.innerHTML = `
    <div class="cv-grid-demo-heading"><strong>观察指标怎样变化</strong><span>合成几何示意</span></div>
    <div class="cv-grid-demo-controls">
      <label for="${id}-size">点阵规格<select id="${id}-size" data-control="size"><option value="3,3">3 × 3 · 9 点</option><option value="5,7" selected>5 × 7 · 35 点</option><option value="7,7">7 × 7 · 49 点</option></select></label>
      <label for="${id}-shape">成像形状<select id="${id}-shape" data-control="shape">${Object.entries(shapeNames).map(([value, text]) => `<option value="${value}"${value === 'pincushion' ? ' selected' : ''}>${text}</option>`).join('')}</select></label>
      <label for="${id}-amount"><span data-label="amount">变形幅度</span><span class="cv-grid-demo-range"><input id="${id}-amount" data-control="amount" type="range" min="0" max="20" step="1" value="12"><output data-value="amount">12%</output></span></label>
    </div>
    <div class="cv-grid-demo-primary" aria-live="polite" aria-atomic="true">
      <div><span>水平 TV</span><output data-metric="horizontalTvPercent"></output></div>
      <div><span>垂直 TV</span><output data-metric="verticalTvPercent"></output></div>
      <div><span>最大边长差比例</span><output data-metric="maximumEdgeLengthDifferencePercent"></output></div>
      <div><span>最大倾斜角</span><output data-metric="maximumTiltDegrees"></output></div>
    </div>
    <svg class="cv-grid-demo-plot" role="img" aria-label="完整实测点阵与代表九点，灰色虚线为角点连线及中心跨度"></svg>
    <div class="cv-grid-demo-legend"><span data-value="selection"></span><span>大点 A～I 为代表九点</span><span>灰虚线为直线参考</span></div>
    <div class="cv-grid-demo-details">
      <dl><div><dt>上边畸变</dt><dd data-metric="topPercent"></dd></div><div><dt>下边畸变</dt><dd data-metric="bottomPercent"></dd></div><div><dt>左边畸变</dt><dd data-metric="leftPercent"></dd></div><div><dt>右边畸变</dt><dd data-metric="rightPercent"></dd></div></dl>
      <dl><div><dt>Keystone Horizontal</dt><dd data-metric="keystoneHorizontalPercent"></dd></div><div><dt>Keystone Vertical</dt><dd data-metric="keystoneVerticalPercent"></dd></div></dl>
    </div>
    <p class="cv-grid-demo-explanation" data-value="explanation"></p>
  `;
  const size = element.querySelector('[data-control="size"]');
  const shape = element.querySelector('[data-control="shape"]');
  const amount = element.querySelector('[data-control="amount"]');
  const plot = element.querySelector('.cv-grid-demo-plot');
  const ns = 'http://www.w3.org/2000/svg';
  const format = (value, unit = '%') => `${(Math.abs(value) < 1e-9 ? 0 : value).toFixed(2)}${unit}`;
  let points, analysis, rows = 5, cols = 7, disposed = false, lastWidth = 0;

  function draw() {
    if (disposed || !points) return;
    const width = Math.max(240, plot.getBoundingClientRect().width || 680);
    const height = width < 460 ? 290 : 350;
    plot.setAttribute('viewBox', `0 0 ${width} ${height}`);
    plot.setAttribute('height', height);
    const minX = Math.min(...points.map(point => point.x)), maxX = Math.max(...points.map(point => point.x));
    const minY = Math.min(...points.map(point => point.y)), maxY = Math.max(...points.map(point => point.y));
    const scale = Math.min((width - 78) / (maxX - minX), (height - 82) / (maxY - minY));
    const map = point => [width / 2 + (point.x - (minX + maxX) / 2) * scale, height / 2 + (point.y - (minY + maxY) / 2) * scale];
    const make = (name, attrs, text) => {
      const node = document.createElementNS(ns, name);
      Object.entries(attrs).forEach(([key, value]) => node.setAttribute(key, String(value)));
      if (text !== undefined) node.textContent = text;
      return node;
    };
    const path = (list, closed, className) => make('path', { d: list.map((point, index) => `${index ? 'L' : 'M'}${map(point).join(' ')}`).join(' ') + (closed ? ' Z' : ''), class: className });
    plot.replaceChildren();
    const [a, b, c, d, , f, g, h, i] = analysis.referencePoints;
    plot.append(path([a, c, i, g], true, 'cv-grid-demo-reference'));
    plot.append(path([d, f], false, 'cv-grid-demo-reference'), path([b, h], false, 'cv-grid-demo-reference'));
    const perimeter = [];
    for (let col = 0; col < cols; col++) perimeter.push(points[col]);
    for (let row = 1; row < rows; row++) perimeter.push(points[row * cols + cols - 1]);
    for (let col = cols - 2; col >= 0; col--) perimeter.push(points[(rows - 1) * cols + col]);
    for (let row = rows - 2; row > 0; row--) perimeter.push(points[row * cols]);
    plot.append(path(perimeter, true, 'cv-grid-demo-measured'));
    const names = new Map(analysis.referencePoints.map(point => [point.id, point.name]));
    for (const point of points) {
      const [x, y] = map(point), name = names.get(point.id);
      plot.append(make('circle', { cx: x, cy: y, r: name ? 4.7 : 2.7, class: name ? 'cv-grid-demo-selected' : 'cv-grid-demo-dot' }));
      if (name) plot.append(make('text', { x, y: y + (point.row === rows - 1 ? 19 : -14), 'text-anchor': 'middle', class: 'cv-grid-demo-point-name' }, name));
    }
  }

  function update() {
    [rows, cols] = size.value.split(',').map(Number);
    points = createGrid(rows, cols, shape.value, Number(amount.value));
    analysis = analyzeGrid(points, rows, cols);
    amount.disabled = shape.value === 'rectangle';
    element.querySelector('[data-label="amount"]').textContent = shape.value === 'rotation' ? '旋转角度' : '变形幅度';
    element.querySelector('[data-value="amount"]').textContent = `${amount.value}${shape.value === 'rotation' ? '°' : '%'}`;
    element.querySelectorAll('[data-metric]').forEach(output => output.textContent = format(analysis[output.dataset.metric], output.dataset.metric === 'maximumTiltDegrees' ? '°' : '%'));
    const rr = [1, (rows + 1) / 2, rows], cc = [1, (cols + 1) / 2, cols];
    element.querySelector('[data-value="selection"]').textContent = `第 ${rr.join('、')} 行 × 第 ${cc.join('、')} 列`;
    element.querySelector('[data-value="explanation"]').textContent = explanations[shape.value];
    element.dataset.shape = shape.value;
    element.dataset.grid = size.value;
    element.dataset.pointCount = String(points.length);
    draw();
  }

  size.addEventListener('change', update);
  shape.addEventListener('change', update);
  amount.addEventListener('input', update);
  const observer = new window.ResizeObserver(entries => {
    const width = entries[0].contentRect.width;
    if (Math.abs(width - lastWidth) > .5) { lastWidth = width; draw(); }
  });
  observer.observe(plot);
  const cleanup = () => {
    disposed = true;
    observer.disconnect();
    size.removeEventListener('change', update);
    shape.removeEventListener('change', update);
    amount.removeEventListener('input', update);
    if (mounts.get(element) === cleanup) mounts.delete(element);
  };
  mounts.set(element, cleanup);
  update();
  return cleanup;
}
