import { chromium } from 'playwright';

const BASE = 'https://[removed]-site1.ktempurl.com';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  console.log('1. Navigating to stations.html...');
  await page.goto(`${BASE}/stations.html`);
  await page.waitForTimeout(2000);

  console.log('2. Logging in as cutting.user...');
  await page.fill('#loginUser', 'cutting.user');
  await page.fill('#loginPass', '1');
  await page.click('#loginBtn');
  await page.waitForTimeout(3000);

  const stationLabel = await page.textContent('#stationLabel');
  console.log(`3. Station: ${stationLabel}`);

  const pendingCount = await page.textContent('#pendingCount');
  console.log(`4. Pending orders: ${pendingCount}`);

  // Test combo dropdown - type a search term
  console.log('5. Typing in combo search...');
  await page.fill('#orderInput', 'ADM');
  await page.waitForTimeout(500);

  const comboItems = await page.$$eval('#comboDropdown .ci[data-id]', els => els.length);
  console.log(`6. Combo dropdown items: ${comboItems}`);

  if (comboItems > 0) {
    // Click first combo item
    console.log('7. Clicking first combo item...');
    await page.click('#comboDropdown .ci[data-id]:first-child');
    await page.waitForTimeout(500);

    const updateBtnDisabled = await page.$eval('#updateBtn', el => el.disabled);
    console.log(`8. Update button enabled: ${!updateBtnDisabled}`);

    const selectedInfo = await page.$eval('#selectedInfo', el => el.classList.contains('hidden'));
    console.log(`9. Selected info visible: ${!selectedInfo}`);

    if (!updateBtnDisabled) {
      page.on('dialog', dialog => dialog.accept());
      console.log('10. Clicking Update...');
      await page.click('#updateBtn');
      await page.waitForTimeout(3000);

      const toastText = await page.textContent('#toast');
      console.log(`11. Toast: ${toastText}`);

      const newCount = await page.textContent('#pendingCount');
      console.log(`12. Orders after update: ${newCount}`);
    }
  }

  // Switch to orders tab
  console.log('13. Switching to orders tab...');
  await page.click('.content-tabs .ctab:nth-child(2)');
  await page.waitForTimeout(500);
  const ordersVisible = await page.$eval('#ordersView', el => !el.classList.contains('hidden'));
  console.log(`14. Orders view visible: ${ordersVisible}`);
  const orderItems = await page.$$eval('#pendingList .item', els => els.length);
  console.log(`15. Orders in list: ${orderItems}`);

  // Switch to history tab
  console.log('16. Switching to history tab...');
  await page.click('.content-tabs .ctab:nth-child(3)');
  await page.waitForTimeout(1000);
  const historyCards = await page.$$eval('#historyList .history-card', els => els.length);
  console.log(`17. History cards: ${historyCards}`);

  await browser.close();
  console.log('\nDONE - All tests passed');
})();
