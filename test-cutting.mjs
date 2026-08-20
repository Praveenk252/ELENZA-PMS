import { chromium } from 'playwright';

const BASE = 'https://[removed]-site1.ktempurl.com';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  console.log('1. Navigating to qr-scanner.html...');
  await page.goto(`${BASE}/qr-scanner.html`);
  await page.waitForTimeout(2000);

  console.log('2. Logging in as cutting.user...');
  await page.fill('#loginUser', 'cutting.user');
  await page.fill('#loginPass', '1');
  await page.click('#loginBtn');
  await page.waitForTimeout(3000);

  // Check what station is selected
  const stationLabel = await page.textContent('#stationLabel');
  console.log(`3. Station: ${stationLabel}`);

  // Check pending count
  const pendingCount = await page.textContent('#pendingCount');
  console.log(`4. Pending orders: ${pendingCount}`);

  // Get all visible orders in the list
  const orderItems = await page.$$eval('#pendingList .item', els => els.map(el => ({
    id: el.getAttribute('data-id'),
    text: el.textContent.trim()
  })));
  console.log(`5. Orders in list: ${orderItems.length}`);
  if (orderItems.length > 0) {
    console.log(`   First 3: ${JSON.stringify(orderItems.slice(0, 3))}`);
  }

  // Try clicking the first order
  if (orderItems.length > 0) {
    const firstId = orderItems[0].id;
    console.log(`6. Clicking order ${firstId}...`);
    await page.click(`#pendingList .item[data-id="${firstId}"]`);
    await page.waitForTimeout(1000);

    // Check action panel
    const actionPanelVisible = await page.$eval('#actionPanel', el => el.classList.contains('show'));
    console.log(`7. Action panel visible: ${actionPanelVisible}`);

    if (actionPanelVisible) {
      const orderNum = await page.textContent('#actionOrderNum');
      console.log(`8. Order in panel: ${orderNum}`);

      // Try clicking Complete
      console.log('9. Clicking Complete...');
      await page.click('.btn-complete');
      await page.waitForTimeout(2000);

      // Check for toast
      const toastText = await page.textContent('#toast');
      console.log(`10. Toast: ${toastText}`);
    }
  }

  await browser.close();
})();
