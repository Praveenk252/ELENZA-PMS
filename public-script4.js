(() => {
  const FILE_MODE = window.location.protocol === "file:";
  const SIDEBAR_BREAKPOINT = 1080;
  const QR_LOCAL_ENABLED = new URLSearchParams(window.location.search).get("qr") === "1";
  const QR_ALLOWED_STATIONS = /* @__PURE__ */ new Set(["Hot Press", "Cutting", "Edgebanding", "Drilling", "Packing"]);
  const API_ACTIONS = {
    "/api/session": "session",
    "/api/login-init": "login-init",
    "/api/login": "login",
    "/api/logout": "logout",
    "/api/app-state": "app-state",
    "/api/dealers": "dealers",
    "/api/dealers/update": "dealers-update",
    "/api/dealers/delete": "dealers-delete",
    "/api/dealers/import": "dealers-import",
    "/api/orders/quotation": "orders-quotation",
    "/api/orders/quotation/delete": "orders-quotation-delete",
    "/api/orders/quotation/import": "orders-quotation-import",
    "/api/orders/confirm": "orders-confirm",
    "/api/orders/optimise": "orders-optimise",
    "/api/orders/procurement": "orders-procurement",
    "/api/planner/save": "planner-save",
    "/api/planner/move": "planner-move",
    "/api/planner/resequence": "planner-resequence",
    "/api/planner/reapprove": "planner-reapprove",
    "/api/planner/assign-station": "planner-assign-station",
    "/api/sequence-profiles/save": "sequence-profiles-save",
    "/api/sequence-profiles/add-station": "sequence-profiles-add-station",
    "/api/sequence-profiles/update-station": "sequence-profiles-update-station",
    "/api/sequence-profiles/reorder-station": "sequence-profiles-reorder-station",
    "/api/sequence-profiles/delete-station": "sequence-profiles-delete-station",
    "/api/production/action": "production-action",
    "/api/production/balance-save": "production-balance-save",
    "/api/packing/boxes-set": "packing-boxes-set",
    "/api/dispatch/action": "dispatch-action",
    "/api/dispatch/balance-save": "dispatch-balance-save",
    "/api/dispatch/boxes/add": "dispatch-boxes-add",
    "/api/dispatch/boxes/state": "dispatch-boxes-state",
    "/api/masters/customer-types": "masters-customer-types",
    "/api/masters/order-types": "masters-order-types",
    "/api/masters/vendors": "masters-vendors",
    "/api/masters/update": "masters-update",
    "/api/masters/dealer-dropdowns": "masters-dealer-dropdowns",
    "/api/masters/dropdown-update": "masters-dropdown-update",
    "/api/masters/dropdown-delete": "masters-dropdown-delete",
    "/api/masters/reorder": "masters-reorder",
    "/api/masters/deactivate": "masters-deactivate",
    "/api/machines/save": "machines-save",
    "/api/users/template": "users-template",
    "/api/users": "users",
    "/api/users/toggle": "users-toggle",
    "/api/users/reset-password": "users-reset-password",
    "/api/users/import": "users-import",
    "/api/mail/send-hourly-production": "mail-send-hourly-production"
  };
  const PLANNER_COLUMNS = [
    { id: "confirmation_date", label: "Confirmation Date" },
    { id: "order_number", label: "Order Number" },
    { id: "customer_name", label: "Customer Name" },
    { id: "customer_type", label: "Customer Type" },
    { id: "order_type", label: "Order Type" },
    { id: "order_class", label: "Main / Sub / Snag / Rework" },
    { id: "material_received_date", label: "Material Rcvd Date" },
    { id: "current_status", label: "Current Status" },
    { id: "edd", label: "EDD" },
    { id: "panel_qty", label: "Panel Qty" },
    { id: "board_qty", label: "Board Qty" },
    { id: "priority", label: "Priority" }
  ];
  const state = {
    session: null,
    app: null,
    ui: {
      productionStation: "",
      productionSearch: "",
      plannerSearch: "",
      plannerStage: "all",
      plannerSort: "rank-asc",
      plannerSortDir: "asc",
      plannerColumnFilters: {},
      reportSearch: "",
      usersSearch: "",
      reportStatus: "all",
      reportDealer: "all",
      reportOrderType: "all",
      reportStation: "all",
      sharedPlanningSearch: "",
      sharedPlanningStage: "all",
      sharedPlanningSort: "rank-asc",
      plannerColumnOrder: [],
      plannerColumnLogin: "",
      plannerDraggingColumn: "",
      selectedSequenceProfileId: "",
      plannerSubtab: "queue",
      plannerExpandedOrderId: null,
      plannerFields: { sla: true, urgency: true, priority: true, remarks: true },
      reportDateFrom: "",
      reportDateTo: "",
      reportSort: "updated-desc",
      productionBalanceOrderId: null,
      selectedOrderId: null,
      dispatchExpandedOrderId: null,
      historyModalOpen: false,
      qrModalOpen: false,
      sidebarExpanded: false,
      sidebarMobileOpen: false,
      pagination: {
        recentOrders: 1,
        dealerRegister: 1,
        quotationRegister: 1,
        optimisation: 1,
        procurement: 1,
        planner: 1,
        production: 1,
        dispatch: 1,
        sharedPlanningDataEntry: 1,
        sharedPlanningOptimisation: 1,
        sharedPlanningProcurement: 1,
        sharedPlanningProduction: 1,
        sharedPlanningDispatch: 1,
        reports: 1,
        audit: 1,
        history: 1,
        users: 1,
        dealerDashboard: 1,
        marketingDashboard: 1,
        emailLog: 1,
        machineReport: 1
      }
    }
  };
  let qrScannerInstance = null;
  let qrScriptLoadingPromise = null;
  const refs = {
    authShell: document.querySelector("#auth-shell"),
    appShell: document.querySelector("#app-shell"),
    sidebarPanel: document.querySelector("#sidebar-panel"),
    sidebarBackdrop: document.querySelector("#sidebar-backdrop"),
    sidebarToggle: document.querySelector("#sidebar-toggle"),
    sidebarDismiss: document.querySelector("#sidebar-dismiss"),
    authMessageStrip: document.querySelector("#auth-message-strip"),
    loginForm: document.querySelector("#login-form"),
    loginUsername: document.querySelector("#login-username"),
    loginPassword: document.querySelector("#login-password"),
    dealerName: document.querySelector("#dealer-name"),
    dealerCompany: document.querySelector("#dealer-company"),
    dealerType: document.querySelector("#dealer-type"),
    dealerCustomerType: document.querySelector("#dealer-customer-type"),
    dealerCode: document.querySelector("#dealer-code"),
    dealerCity: document.querySelector("#dealer-city"),
    dealerPinCode: document.querySelector("#dealer-pin-code"),
    dealerGst: document.querySelector("#dealer-gst"),
    dealerContact: document.querySelector("#dealer-contact"),
    dealerMobile: document.querySelector("#dealer-mobile"),
    dealerEmail: document.querySelector("#dealer-email"),
    dealerPaymentTerms: document.querySelector("#dealer-payment-terms"),
    dealerCreditLimit: document.querySelector("#dealer-credit-limit"),
    dealerMarketingOwner: document.querySelector("#dealer-marketing-owner"),
    dealerAddress: document.querySelector("#dealer-address"),
    dealerImportFile: document.querySelector("#dealer-import-file"),
    dealerImportForm: document.querySelector("#dealer-import-form"),
    downloadDealerTemplate: document.querySelector("#download-dealer-template"),
    quotationDealer: document.querySelector("#quotation-dealer"),
    quotationCustomerType: document.querySelector("#quotation-customer-type"),
    quotationMainOrder: document.querySelector("#quotation-main-order"),
    quotationSubOrderWrap: document.querySelector("#quotation-sub-order-wrap"),
    quotationSubOrder: document.querySelector("#quotation-sub-order"),
    quotationImportFile: document.querySelector("#quotation-import-file"),
    quotationImportForm: document.querySelector("#quotation-import-form"),
    downloadQuotationTemplate: document.querySelector("#download-quotation-template"),
    quotationOrderNumber: document.querySelector("#quotation-order-number"),
    quotationOrderNumberCount: document.querySelector("#quotation-order-number-count"),
    passwordResetForm: document.querySelector("#password-reset-form"),
    passwordResetLogin: document.querySelector("#password-reset-login"),
    passwordResetPassword: document.querySelector("#password-reset-password"),
    sessionRoleTag: document.querySelector("#session-role-tag"),
    sessionName: document.querySelector("#session-name"),
    sessionMeta: document.querySelector("#session-meta"),
    sectionTitle: document.querySelector("#section-title"),
    messageStrip: document.querySelector("#message-strip"),
    refreshData: document.querySelector("#refresh-data"),
    logoutButton: document.querySelector("#logout-button"),
    customerTypeChipWrap: document.querySelector("#customer-type-chip-wrap"),
    recentOrdersBody: document.querySelector("#recent-orders-body"),
    recentOrdersCount: document.querySelector("#recent-orders-count"),
    recentOrdersPagination: document.querySelector("#recent-orders-pagination"),
    recentOrdersSearch: document.querySelector("#recent-orders-search"),
    dealerRegisterBody: document.querySelector("#dealer-register-body"),
    dealerCount: document.querySelector("#dealer-count"),
    dealerPagination: document.querySelector("#dealer-pagination"),
    dealerRegisterSearch: document.querySelector("#dealer-register-search"),
    quotationRegisterSearch: document.querySelector("#quotation-register-search"),
    quotationRegisterBody: document.querySelector("#quotation-register-body"),
    quotationCount: document.querySelector("#quotation-count"),
    quotationPagination: document.querySelector("#quotation-pagination"),
    confirmDateTime: document.querySelector("#confirm-date-time"),
    optimisationForm: document.querySelector("#optimisation-form"),
    optimisationDateTime: document.querySelector("#optimisation-date-time"),
    optimisationOrderNumber: document.querySelector("#optimisation-order-number"),
    optimisationBoards: document.querySelector("#optimisation-boards"),
    optimisationPanels: document.querySelector("#optimisation-panels"),
    optimisationRmDetails: document.querySelector("#optimisation-rm-details"),
    optimisationBody: document.querySelector("#optimisation-table-body"),
    optimisationCount: document.querySelector("#optimisation-count"),
    optimisationSearch: document.querySelector("#optimisation-search"),
    exportOptimisation: document.querySelector("#export-optimisation"),
    optimisationPagination: document.querySelector("#optimisation-pagination"),
    procurementForm: document.querySelector("#procurement-form"),
    procurementOrderNumber: document.querySelector("#procurement-order-number"),
    procurementPoNumber: document.querySelector("#procurement-po-number"),
    procurementPoDate: document.querySelector("#procurement-po-date"),
    procurementVendor: document.querySelector("#procurement-vendor"),
    procurementMrnDate: document.querySelector("#procurement-mrn-date"),
    procurementItemDetails: document.querySelector("#procurement-item-details"),
    procurementRemarks: document.querySelector("#procurement-remarks"),
    procurementBody: document.querySelector("#procurement-table-body"),
    procurementCount: document.querySelector("#procurement-count"),
    procurementSearch: document.querySelector("#procurement-search"),
    procurementPagination: document.querySelector("#procurement-pagination"),
    plannerBody: document.querySelector("#planner-body"),
    plannerCount: document.querySelector("#planner-count"),
    plannerPagination: document.querySelector("#planner-pagination"),
    plannerFieldToolbar: document.querySelector("#planner-field-toolbar"),
    plannerQueuePanel: document.querySelector("#planner-queue-panel"),
    plannerMovePanel: document.querySelector("#planner-move-panel"),
    plannerSequencePanel: document.querySelector("#planner-sequence-panel"),
    plannerMoveBody: document.querySelector("#planner-move-body"),
    plannerMoveCount: document.querySelector("#planner-move-count"),
    plannerMovePagination: document.querySelector("#planner-move-pagination"),
    plannerSearch: document.querySelector("#planner-search"),
    plannerStageFilter: document.querySelector("#planner-stage-filter"),
    exportPlanner: document.querySelector("#export-planner"),
    exportPlannerMove: document.querySelector("#export-planner-move"),
    resetPlannerColumns: document.querySelector("#reset-planner-columns"),
    plannerHeadRow: document.querySelector("#planner-head-row"),
    plannerProfileForm: document.querySelector("#planner-profile-form"),
    plannerProfileName: document.querySelector("#planner-profile-name"),
    plannerSequenceOrderType: document.querySelector("#planner-sequence-order-type"),
    plannerSequenceOrderClass: document.querySelector("#planner-sequence-order-class"),
    plannerProfileSelect: document.querySelector("#planner-profile-select"),
    plannerMachineForm: document.querySelector("#planner-machine-form"),
    plannerMachineName: document.querySelector("#planner-machine-name"),
    plannerSequenceForm: document.querySelector("#planner-sequence-form"),
    plannerSequenceStation: document.querySelector("#planner-sequence-station"),
    plannerMachineSequenceList: document.querySelector("#planner-machine-sequence-list"),
    plannerStationMasterList: document.querySelector("#planner-station-master-list"),
    sendHourlyProductionMail: document.querySelector("#send-hourly-production-mail"),
    productionStationFilter: document.querySelector("#production-station-filter"),
    productionSearch: document.querySelector("#production-search"),
    productionActionForm: document.querySelector("#production-action-form"),
    productionActionOrder: document.querySelector("#production-action-order"),
    productionQrLaunch: document.querySelector("#production-qr-launch"),
    productionQrNote: document.querySelector("#production-qr-note"),
    productionActionOrderHelper: document.querySelector("#production-action-order-helper"),
    productionActionRemarks: document.querySelector("#production-action-remarks"),
    packingBoxForm: document.querySelector("#packing-box-form"),
    packingBoxOrder: document.querySelector("#packing-box-order"),
    packingBoxQtyForm: document.querySelector("#packing-box-qty-form"),
    packingBoxFormEmpty: document.querySelector("#packing-box-form-empty"),
    productionBody: document.querySelector("#production-table-body"),
    productionCount: document.querySelector("#production-count"),
    productionPagination: document.querySelector("#production-pagination"),
    sharedPlanningSearch: document.querySelector("#shared-planning-search"),
    sharedPlanningStage: document.querySelector("#shared-planning-stage"),
    sharedPlanningSort: document.querySelector("#shared-planning-sort"),
    exportSharedPlanning: document.querySelector("#export-shared-planning"),
    dispatchBody: document.querySelector("#dispatch-table-body"),
    dispatchCount: document.querySelector("#dispatch-count"),
    dispatchSearch: document.querySelector("#dispatch-search"),
    dispatchPagination: document.querySelector("#dispatch-pagination"),
    sharedPlanningDataEntryBody: document.querySelector("#shared-planning-data-entry-body"),
    sharedPlanningDataEntryCount: document.querySelector("#shared-planning-count-data-entry"),
    sharedPlanningDataEntryPagination: document.querySelector("#shared-planning-data-entry-pagination"),
    sharedPlanningDataEntrySearch: document.querySelector("#shared-planning-data-entry-search"),
    sharedPlanningOptimisationBody: document.querySelector("#shared-planning-optimisation-body"),
    sharedPlanningOptimisationCount: document.querySelector("#shared-planning-count-optimisation"),
    sharedPlanningOptimisationPagination: document.querySelector("#shared-planning-optimisation-pagination"),
    sharedPlanningOptimisationSearch: document.querySelector("#shared-planning-optimisation-search"),
    sharedPlanningProcurementBody: document.querySelector("#shared-planning-procurement-body"),
    sharedPlanningProcurementCount: document.querySelector("#shared-planning-count-procurement"),
    sharedPlanningProcurementPagination: document.querySelector("#shared-planning-procurement-pagination"),
    sharedPlanningProcurementSearch: document.querySelector("#shared-planning-procurement-search"),
    sharedPlanningProductionBody: document.querySelector("#shared-planning-production-body"),
    sharedPlanningProductionCount: document.querySelector("#shared-planning-count-production"),
    sharedPlanningProductionPagination: document.querySelector("#shared-planning-production-pagination"),
    sharedPlanningDispatchBody: document.querySelector("#shared-planning-dispatch-body"),
    sharedPlanningDispatchCount: document.querySelector("#shared-planning-count-dispatch"),
    sharedPlanningDispatchPagination: document.querySelector("#shared-planning-dispatch-pagination"),
    sharedPlanningDispatchSearch: document.querySelector("#shared-planning-dispatch-search"),
    reportsBody: document.querySelector("#reports-body"),
    dealerDashboardBody: document.querySelector("#dealer-dashboard-body"),
    dealerDashboardCount: document.querySelector("#dealer-dashboard-count"),
    dealerDashboardPagination: document.querySelector("#dealer-dashboard-pagination"),
    dealerDashboardSearch: document.querySelector("#dealer-dashboard-search"),
    marketingDashboardBody: document.querySelector("#marketing-dashboard-body"),
    marketingDashboardCount: document.querySelector("#marketing-dashboard-count"),
    marketingDashboardPagination: document.querySelector("#marketing-dashboard-pagination"),
    marketingDashboardSearch: document.querySelector("#marketing-dashboard-search"),
    reportCount: document.querySelector("#report-count"),
    reportsPagination: document.querySelector("#reports-pagination"),
    reportSearch: document.querySelector("#report-search"),
    reportStatusFilter: document.querySelector("#report-status-filter"),
    reportDealerFilter: document.querySelector("#report-dealer-filter"),
    reportOrderTypeFilter: document.querySelector("#report-order-type-filter"),
    reportStationFilter: document.querySelector("#report-station-filter"),
    reportDateFrom: document.querySelector("#report-date-from"),
    reportDateTo: document.querySelector("#report-date-to"),
    reportSort: document.querySelector("#report-sort"),
    reportLast7: document.querySelector("#report-last-7"),
    exportReport: document.querySelector("#export-report"),
    weeklyRangeLabel: document.querySelector("#weekly-range-label"),
    weeklyMetrics: document.querySelector("#weekly-metrics"),
    weeklyDailyBody: document.querySelector("#weekly-daily-body"),
    weeklyDailySearch: document.querySelector("#weekly-daily-search"),
    weeklyModuleBody: document.querySelector("#weekly-module-body"),
    weeklyModuleCount: document.querySelector("#weekly-module-count"),
    weeklyModuleSearch: document.querySelector("#weekly-module-search"),
    weeklyRecentBody: document.querySelector("#weekly-recent-body"),
    weeklyRecentCount: document.querySelector("#weekly-recent-count"),
    weeklyRecentSearch: document.querySelector("#weekly-recent-search"),
    lifecycleTitle: document.querySelector("#lifecycle-title"),
    lifecycleDetail: document.querySelector("#lifecycle-detail"),
    auditBody: document.querySelector("#audit-body"),
    auditCount: document.querySelector("#audit-count"),
    auditPagination: document.querySelector("#audit-pagination"),
    auditSearch: document.querySelector("#audit-search"),
    historyBody: document.querySelector("#history-body"),
    historyCount: document.querySelector("#history-count"),
    historyPagination: document.querySelector("#history-pagination"),
    historySearch: document.querySelector("#history-search"),
    historyLifecycleTitle: document.querySelector("#history-lifecycle-title"),
    historyLifecycleDetail: document.querySelector("#history-lifecycle-detail"),
    emailLogBody: document.querySelector("#email-log-body"),
    emailLogCount: document.querySelector("#email-log-count"),
    emailLogPagination: document.querySelector("#email-log-pagination"),
    emailLogSearch: document.querySelector("#email-log-search"),
    machineReportSearch: document.querySelector("#machine-report-search"),
    dealerTypeList: document.querySelector("#dealer-type-list"),
    paymentTermsList: document.querySelector("#payment-terms-list"),
    marketingOwnerList: document.querySelector("#marketing-owner-list"),
    quotationOwnerList: document.querySelector("#quotation-owner-list"),
    orderClassList: document.querySelector("#order-class-list"),
    customerTypeList: document.querySelector("#customer-type-list"),
    orderTypeList: document.querySelector("#order-type-list"),
    vendorList: document.querySelector("#vendor-list"),
    machineSequenceList: document.querySelector("#machine-sequence-list"),
    usersBody: document.querySelector("#users-body"),
    usersPagination: document.querySelector("#users-pagination"),
    usersSearch: document.querySelector("#users-search"),
    dealerOptions: document.querySelector("#dealer-options"),
    dealerTypeOptions: document.querySelector("#dealer-type-options"),
    paymentTermsOptions: document.querySelector("#payment-terms-options"),
    marketingOwnerOptions: document.querySelector("#marketing-owner-options"),
    quotationOwnerOptions: document.querySelector("#quotation-owner-options"),
    customerTypeOptions: document.querySelector("#customer-type-options"),
    orderTypeOptions: document.querySelector("#order-type-options"),
    orderClassOptions: document.querySelector("#order-class-options"),
    vendorOptions: document.querySelector("#vendor-options"),
    confirmOrderOptions: document.querySelector("#confirm-order-options"),
    optimisationOrderOptions: document.querySelector("#optimisation-order-options"),
    procurementOrderOptions: document.querySelector("#procurement-order-options"),
    productionActionOrderOptions: document.querySelector("#production-action-order-options"),
    packingBoxOrderOptions: document.querySelector("#packing-box-order-options"),
    procurementStatus: document.querySelector("#procurement-status"),
    userRole: document.querySelector("#user-role"),
    userStation: document.querySelector("#user-station"),
    userId: document.querySelector("#user-id"),
    userName: document.querySelector("#user-name"),
    userLogin: document.querySelector("#user-login"),
    userPassword: document.querySelector("#user-password"),
    userSaveButton: document.querySelector("#user-save-button"),
    userCancelEdit: document.querySelector("#user-cancel-edit"),
    currentBuildList: document.querySelector("#current-build-list"),
    pathBuildList: document.querySelector("#path-build-list"),
    historyModal: document.querySelector("#history-modal"),
    historyModalBackdrop: document.querySelector("#history-modal-backdrop"),
    historyModalClose: document.querySelector("#history-modal-close"),
    historyModalTitle: document.querySelector("#history-modal-title"),
    historyModalBody: document.querySelector("#history-modal-body"),
    qrModal: document.querySelector("#qr-modal"),
    qrModalBackdrop: document.querySelector("#qr-modal-backdrop"),
    qrModalClose: document.querySelector("#qr-modal-close"),
    qrModalCopy: document.querySelector("#qr-modal-copy"),
    qrManualInput: document.querySelector("#qr-manual-input"),
    qrManualApply: document.querySelector("#qr-manual-apply"),
    qrRestart: document.querySelector("#qr-restart"),
    qrResultHelper: document.querySelector("#qr-result-helper"),
    dealerEntryCard: document.querySelector("#dealer-entry-card"),
    quotationEntryCard: document.querySelector("#quotation-entry-card"),
    confirmEntryCard: document.querySelector("#confirm-entry-card"),
    recentOrdersCard: document.querySelector("#recent-orders-card"),
    dealerRegisterCard: document.querySelector("#dealer-register-card"),
    quotationRegisterCard: document.querySelector("#quotation-register-card"),
    sharedPlanningDataEntryCard: document.querySelector("#shared-planning-data-entry-card")
  };
  init();
  async function init() {
    bindEvents();
    if (FILE_MODE) {
      renderLocalOnlyMessage();
      return;
    }
    await restoreSession();
  }
  function bindEvents() {
    var _a, _b, _c, _d, _e, _f, _g, _h, _i, _j, _k, _l, _m, _n, _o, _p, _q, _r, _s, _t, _u, _v, _w, _x, _y, _z, _A, _B, _C, _D, _E, _F, _G, _H, _I, _J, _K, _L, _M, _N, _O, _P, _Q, _R, _S, _T;
    document.querySelectorAll(".nav-link").forEach((button) => {
      button.addEventListener("click", () => activateSection(button.dataset.section));
    });
    refs.sidebarToggle.addEventListener("click", onSidebarToggle);
    refs.sidebarDismiss.addEventListener("click", closeSidebar);
    refs.sidebarBackdrop.addEventListener("click", closeSidebar);
    refs.loginForm.addEventListener("submit", onLogin);
    refs.refreshData.addEventListener("click", () => loadAppState(false));
    refs.logoutButton.addEventListener("click", onLogout);
    document.querySelector("#dealer-form").addEventListener("submit", onDealerSave);
    refs.dealerImportForm.addEventListener("submit", onDealerImport);
    refs.downloadDealerTemplate.addEventListener("click", downloadDealerTemplate);
    (_a = refs.dealerRegisterSearch) == null ? void 0 : _a.addEventListener("input", debounce(() => {
      state.ui.pagination.dealerRegister = 1;
      renderDataEntry();
    }, 180));
    (_b = refs.quotationRegisterSearch) == null ? void 0 : _b.addEventListener("input", debounce(() => {
      state.ui.pagination.quotationRegister = 1;
      renderDataEntry();
    }, 180));
    (_c = refs.recentOrdersSearch) == null ? void 0 : _c.addEventListener("input", debounce(() => {
      state.ui.pagination.recentOrders = 1;
      renderDataEntry();
    }, 180));
    (_d = refs.optimisationSearch) == null ? void 0 : _d.addEventListener("input", debounce(() => {
      state.ui.pagination.optimisation = 1;
      renderAll();
    }, 180));
    (_e = refs.procurementSearch) == null ? void 0 : _e.addEventListener("input", debounce(() => {
      state.ui.pagination.procurement = 1;
      renderAll();
    }, 180));
    (_f = refs.exportOptimisation) == null ? void 0 : _f.addEventListener("click", exportOptimisationExcel);
    refs.dealerRegisterBody.addEventListener("click", onDealerRegisterClick);
    document.querySelector("#quotation-form").addEventListener("submit", onQuotationSave);
    refs.quotationImportForm.addEventListener("submit", onQuotationImport);
    refs.downloadQuotationTemplate.addEventListener("click", downloadQuotationTemplate);
    refs.quotationRegisterBody.addEventListener("click", onQuotationRegisterClick);
    (_g = refs.quotationOrderNumber) == null ? void 0 : _g.addEventListener("input", updateQuotationOrderNumberCounter);
    document.querySelector("#confirm-form").addEventListener("submit", onConfirmOrder);
    document.querySelector("#optimisation-form").addEventListener("submit", onOptimisationSave);
    document.querySelector("#procurement-form").addEventListener("submit", onProcurementSave);
    refs.plannerProfileForm.addEventListener("submit", onPlannerProfileSave);
    refs.plannerSequenceForm.addEventListener("submit", onPlannerSequenceAddStation);
    refs.plannerMachineForm.addEventListener("submit", onPlannerMachineSave);
    document.querySelector("#customer-type-form").addEventListener("submit", onCustomerTypeSave);
    document.querySelector("#order-type-form").addEventListener("submit", onOrderTypeSave);
    document.querySelector("#vendor-form").addEventListener("submit", onVendorSave);
    document.querySelector("#user-form").addEventListener("submit", onUserSave);
    (_h = refs.passwordResetForm) == null ? void 0 : _h.addEventListener("submit", onPasswordReset);
    document.querySelector("#user-import-form").addEventListener("submit", onUserImport);
    document.querySelectorAll(".dropdown-master-form").forEach((form) => {
      form.addEventListener("submit", onDropdownMasterSave);
    });
    refs.productionStationFilter.addEventListener("change", () => {
      state.ui.productionStation = refs.productionStationFilter.value;
      state.ui.pagination.production = 1;
      loadAppState(false);
    });
    (_i = refs.productionActionForm) == null ? void 0 : _i.addEventListener("click", onProductionActionFormClick);
    (_j = refs.productionActionOrder) == null ? void 0 : _j.addEventListener("change", () => {
      var _a2, _b2;
      return renderProductionActionForm(((_b2 = (_a2 = state.app) == null ? void 0 : _a2.production) == null ? void 0 : _b2.rows) || []);
    });
    (_k = refs.productionActionOrder) == null ? void 0 : _k.addEventListener("blur", () => {
      var _a2, _b2;
      return renderProductionActionForm(((_b2 = (_a2 = state.app) == null ? void 0 : _a2.production) == null ? void 0 : _b2.rows) || []);
    });
    (_l = refs.productionQrLaunch) == null ? void 0 : _l.addEventListener("click", openQrModal);
    (_m = refs.packingBoxForm) == null ? void 0 : _m.addEventListener("submit", onPackingBoxFormSave);
    (_n = refs.packingBoxOrder) == null ? void 0 : _n.addEventListener("input", () => {
      var _a2, _b2;
      return renderPackingBoxForm(((_b2 = (_a2 = state.app) == null ? void 0 : _a2.production) == null ? void 0 : _b2.rows) || []);
    });
    refs.productionSearch.addEventListener("input", debounce(() => {
      state.ui.productionSearch = refs.productionSearch.value.trim();
      state.ui.pagination.production = 1;
      loadAppState(false);
    }, 250));
    refs.reportSearch.addEventListener("input", debounce(() => {
      state.ui.reportSearch = refs.reportSearch.value.trim();
      state.ui.pagination.reports = 1;
      loadAppState(false);
    }, 250));
    refs.reportStatusFilter.addEventListener("change", syncReportFilters);
    refs.reportDealerFilter.addEventListener("change", syncReportFilters);
    refs.reportOrderTypeFilter.addEventListener("change", syncReportFilters);
    refs.reportStationFilter.addEventListener("change", syncReportFilters);
    refs.reportDateFrom.addEventListener("change", syncReportFilters);
    refs.reportDateTo.addEventListener("change", syncReportFilters);
    refs.reportSort.addEventListener("change", syncReportFilters);
    refs.reportLast7.addEventListener("click", applyLast7DaysFilter);
    refs.exportReport.addEventListener("click", exportReportCsv);
    (_o = refs.exportPlanner) == null ? void 0 : _o.addEventListener("click", exportPlannerExcel);
    (_p = refs.exportPlannerMove) == null ? void 0 : _p.addEventListener("click", exportPlannerMoveExcel);
    (_q = refs.resetPlannerColumns) == null ? void 0 : _q.addEventListener("click", resetPlannerColumnOrder);
    (_r = refs.sharedPlanningDataEntrySearch) == null ? void 0 : _r.addEventListener("input", debounce(syncSharedPlanningDataEntryFilters, 180));
    (_s = refs.sharedPlanningOptimisationSearch) == null ? void 0 : _s.addEventListener("input", debounce(() => {
      state.ui.pagination.sharedPlanningOptimisation = 1;
      renderOptimisation();
    }, 180));
    (_t = refs.sharedPlanningProcurementSearch) == null ? void 0 : _t.addEventListener("input", debounce(() => {
      state.ui.pagination.sharedPlanningProcurement = 1;
      renderProcurement();
    }, 180));
    (_u = refs.sharedPlanningDispatchSearch) == null ? void 0 : _u.addEventListener("input", debounce(() => {
      state.ui.pagination.sharedPlanningDispatch = 1;
      renderDispatch();
    }, 180));
    refs.sharedPlanningSearch.addEventListener("input", debounce(syncSharedPlanningFilters, 180));
    refs.sharedPlanningStage.addEventListener("change", syncSharedPlanningFilters);
    refs.sharedPlanningSort.addEventListener("change", syncSharedPlanningFilters);
    refs.exportSharedPlanning.addEventListener("click", exportSharedPlanningExcel);
    (_v = refs.plannerSearch) == null ? void 0 : _v.addEventListener("input", debounce(syncPlannerFilters, 180));
    (_w = refs.plannerStageFilter) == null ? void 0 : _w.addEventListener("change", syncPlannerFilters);
    refs.plannerProfileSelect.addEventListener("change", onPlannerProfileSelectChange);
    document.querySelectorAll("[data-planner-subtab]").forEach((button) => {
      button.addEventListener("click", () => {
        state.ui.plannerSubtab = button.dataset.plannerSubtab || "queue";
        renderPlannerSubtab();
      });
    });
    refs.userRole.addEventListener("change", renderUserStationOptions);
    (_x = refs.usersSearch) == null ? void 0 : _x.addEventListener("input", debounce(() => {
      state.ui.usersSearch = refs.usersSearch.value.trim();
      state.ui.pagination.users = 1;
      renderUsers();
    }, 180));
    (_y = refs.dispatchSearch) == null ? void 0 : _y.addEventListener("input", debounce(() => {
      state.ui.pagination.dispatch = 1;
      renderDispatch();
    }, 180));
    (_z = refs.emailLogSearch) == null ? void 0 : _z.addEventListener("input", debounce(() => {
      state.ui.pagination.emailLog = 1;
      renderEmailLog();
    }, 180));
    (_A = refs.dealerDashboardSearch) == null ? void 0 : _A.addEventListener("input", debounce(() => {
      state.ui.pagination.dealerDashboard = 1;
      renderReports();
    }, 180));
    (_B = refs.marketingDashboardSearch) == null ? void 0 : _B.addEventListener("input", debounce(() => {
      state.ui.pagination.marketingDashboard = 1;
      renderReports();
    }, 180));
    (_C = refs.machineReportSearch) == null ? void 0 : _C.addEventListener("input", debounce(() => {
      state.ui.pagination.machineReport = 1;
      renderReports();
    }, 180));
    (_D = refs.weeklyDailySearch) == null ? void 0 : _D.addEventListener("input", debounce(renderReports, 180));
    (_E = refs.weeklyModuleSearch) == null ? void 0 : _E.addEventListener("input", debounce(renderReports, 180));
    (_F = refs.weeklyRecentSearch) == null ? void 0 : _F.addEventListener("input", debounce(renderReports, 180));
    (_G = refs.auditSearch) == null ? void 0 : _G.addEventListener("input", debounce(() => {
      state.ui.pagination.audit = 1;
      renderReports();
    }, 180));
    (_H = refs.historySearch) == null ? void 0 : _H.addEventListener("input", debounce(() => {
      state.ui.pagination.history = 1;
      renderHistory();
    }, 180));
    (_I = refs.userCancelEdit) == null ? void 0 : _I.addEventListener("click", resetUserForm);
    refs.productionBody.addEventListener("click", onProductionTableClick);
    refs.plannerBody.addEventListener("click", onPlannerTableClick);
    refs.plannerMoveBody.addEventListener("click", onPlannerTableClick);
    refs.plannerBody.addEventListener("change", onPlannerTableChange);
    refs.plannerBody.addEventListener("dragstart", onPlannerDragStart);
    refs.plannerBody.addEventListener("dragover", onPlannerDragOver);
    refs.plannerBody.addEventListener("drop", onPlannerDrop);
    (_J = refs.plannerHeadRow) == null ? void 0 : _J.addEventListener("dragstart", onPlannerColumnDragStart);
    (_K = refs.plannerHeadRow) == null ? void 0 : _K.addEventListener("dragover", onPlannerColumnDragOver);
    (_L = refs.plannerHeadRow) == null ? void 0 : _L.addEventListener("drop", onPlannerColumnDrop);
    (_M = refs.plannerHeadRow) == null ? void 0 : _M.addEventListener("click", onPlannerHeaderClick);
    (_N = refs.plannerHeadRow) == null ? void 0 : _N.addEventListener("input", debounce(onPlannerHeaderFilterChange, 180));
    (_O = refs.plannerHeadRow) == null ? void 0 : _O.addEventListener("change", onPlannerHeaderFilterChange);
    refs.dispatchBody.addEventListener("click", onDispatchTableClick);
    refs.reportsBody.addEventListener("click", onReportsTableClick);
    refs.historyBody.addEventListener("click", onHistoryTableClick);
    refs.customerTypeList.addEventListener("click", onMasterListClick);
    refs.orderTypeList.addEventListener("click", onMasterListClick);
    refs.vendorList.addEventListener("click", onMasterListClick);
    refs.machineSequenceList.addEventListener("click", onPlannerMachineListClick);
    document.querySelectorAll("[data-dropdown-list]").forEach((list) => {
      list.addEventListener("click", onDropdownMasterListClick);
    });
    [
      refs.sharedPlanningDataEntryBody,
      refs.sharedPlanningOptimisationBody,
      refs.sharedPlanningProcurementBody,
      refs.sharedPlanningProductionBody,
      refs.sharedPlanningDispatchBody
    ].forEach((body) => body == null ? void 0 : body.addEventListener("click", onSharedPlanningTableClick));
    refs.plannerMachineSequenceList.addEventListener("click", onPlannerMachineListClick);
    (_P = refs.sendHourlyProductionMail) == null ? void 0 : _P.addEventListener("click", onSendHourlyProductionMail);
    refs.usersBody.addEventListener("click", onUsersTableClick);
    document.addEventListener("click", onPaginationClick);
    refs.dealerType.addEventListener("input", refreshDealerCodePreview);
    refs.quotationDealer.addEventListener("input", syncQuotationDealerFields);
    refs.quotationDealer.addEventListener("change", syncQuotationDealerFields);
    refs.quotationMainOrder.addEventListener("input", syncQuotationOrderClassFields);
    refs.quotationMainOrder.addEventListener("change", syncQuotationOrderClassFields);
    document.addEventListener("click", onQuickAddClick);
    document.querySelectorAll("[data-collapse-toggle]").forEach((button) => {
      button.addEventListener("click", onCollapseToggle);
    });
    refs.historyModalClose.addEventListener("click", closeHistoryModal);
    refs.historyModalBackdrop.addEventListener("click", closeHistoryModal);
    (_Q = refs.qrModalClose) == null ? void 0 : _Q.addEventListener("click", closeQrModal);
    (_R = refs.qrModalBackdrop) == null ? void 0 : _R.addEventListener("click", closeQrModal);
    (_S = refs.qrManualApply) == null ? void 0 : _S.addEventListener("click", applyManualQrValue);
    (_T = refs.qrRestart) == null ? void 0 : _T.addEventListener("click", restartQrScanner);
    document.addEventListener("dblclick", onGlobalDblClick);
    document.addEventListener("keydown", onGlobalKeydown);
    window.addEventListener("resize", debounce(syncSidebarViewport, 120));
  }
  async function restoreSession() {
    try {
      const result = await apiGet("/api/session", false);
      if (result.authenticated) {
        state.session = result.user;
        await loadAppState(true);
        showMessage("Session restored.", "success");
      } else {
        renderLoggedOut();
      }
    } catch (e) {
      renderLoggedOut();
    }
  }
  async function onLogin(event) {
    event.preventDefault();
    try {
      const username = refs.loginUsername.value.trim().toLowerCase();
      const password = refs.loginPassword.value;
      const result = await apiPost("/api/login", {
        username,
        password
      });
      state.session = result.user;
      refs.loginPassword.value = "";
      await loadAppState(true);
      showMessage(`Logged in as ${result.user.full_name}.`, "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onLogout() {
    try {
      await apiPost("/api/logout", {});
    } catch (e) {
    }
    state.session = null;
    state.app = null;
    state.ui.selectedOrderId = null;
    renderLoggedOut();
    showMessage("Logged out.", "success");
  }
  async function loadAppState(useHomeSection) {
    var _a, _b, _c, _d, _e;
    if (!state.session) {
      return;
    }
    try {
      const result = await apiGet("/api/app-state", true, {
        production_station: state.ui.productionStation,
        production_search: state.ui.productionSearch,
        report_search: state.ui.reportSearch,
        report_status: state.ui.reportStatus,
        report_dealer: state.ui.reportDealer,
        report_order_type: state.ui.reportOrderType,
        report_station: state.ui.reportStation,
        report_date_from: state.ui.reportDateFrom,
        report_date_to: state.ui.reportDateTo,
        report_sort: state.ui.reportSort,
        selected_order_id: state.ui.selectedOrderId || ""
      });
      state.app = result;
      state.session = result.session;
      state.ui.selectedOrderId = (_e = (_d = (_c = (_a = result.reports) == null ? void 0 : _a.selected_order_id) != null ? _c : (_b = result.history) == null ? void 0 : _b.selected_order_id) != null ? _d : state.ui.selectedOrderId) != null ? _e : null;
      renderAll();
      if (useHomeSection) {
        openRoleHome();
      }
    } catch (error) {
      if (error.message === "Login required.") {
        state.session = null;
        state.app = null;
        renderLoggedOut();
      }
      showMessage(error.message, "error");
    }
  }
  function renderLoggedOut() {
    state.ui.sidebarMobileOpen = false;
    refs.authShell.classList.remove("hidden");
    refs.appShell.classList.add("hidden");
    refs.sessionRoleTag.textContent = "Secure";
    refs.sessionName.textContent = "Not Logged In";
    refs.sessionMeta.textContent = "Sign in to continue";
    refs.sectionTitle.textContent = "Data Entry";
    refs.messageStrip.classList.add("hidden");
    refs.customerTypeChipWrap.innerHTML = "";
    clearTable(refs.recentOrdersBody, 6, "Sign in to load orders.");
    clearTable(refs.dealerRegisterBody, 9, "Sign in to load dealers.");
    clearTable(refs.quotationRegisterBody, 6, "Sign in to load quotations.");
    clearTable(refs.optimisationBody, 5, "Sign in to load optimisation queue.");
    clearTable(refs.procurementBody, 5, "Sign in to load procurement queue.");
    clearTable(refs.plannerBody, 5, "Sign in to load planner queue.");
    clearTable(refs.plannerMoveBody, 6, "Sign in to load planner move page.");
    clearTable(refs.productionBody, 11, "Sign in to load production queue.");
    clearTable(refs.dispatchBody, 8, "Sign in to load dispatch queue.");
    clearTable(refs.sharedPlanningDataEntryBody, 9, "Sign in to load planner view.");
    clearTable(refs.sharedPlanningOptimisationBody, 9, "Sign in to load planner view.");
    clearTable(refs.sharedPlanningProcurementBody, 9, "Sign in to load planner view.");
    clearTable(refs.sharedPlanningProductionBody, 9, "Sign in to load planner view.");
    clearTable(refs.sharedPlanningDispatchBody, 9, "Sign in to load planner view.");
    clearTable(refs.reportsBody, 9, "Sign in to load reports.");
    clearTable(refs.auditBody, 6, "Sign in to load audit logs.");
    clearTable(refs.historyBody, 7, "Sign in to load movement history.");
    refs.lifecycleTitle.textContent = "No Order Selected";
    refs.lifecycleDetail.innerHTML = detailPlaceholder("Sign in to inspect lifecycle.");
    refs.historyLifecycleTitle.textContent = "No Order Selected";
    refs.historyLifecycleDetail.innerHTML = detailPlaceholder("Sign in to inspect lifecycle.");
    refs.dealerTypeList.innerHTML = "";
    refs.paymentTermsList.innerHTML = "";
    refs.marketingOwnerList.innerHTML = "";
    refs.quotationOwnerList.innerHTML = "";
    refs.orderClassList.innerHTML = "";
    refs.customerTypeList.innerHTML = "";
    refs.orderTypeList.innerHTML = "";
    refs.vendorList.innerHTML = "";
    refs.machineSequenceList.innerHTML = "";
    refs.plannerMachineSequenceList.innerHTML = "";
    if (refs.plannerStationMasterList) refs.plannerStationMasterList.innerHTML = "";
    refs.usersBody.innerHTML = emptyRow(6, "Sign in to load users.");
    refs.dealerCount.textContent = "0 Dealers";
    refs.quotationCount.textContent = "0 Quotations";
    refs.recentOrdersCount.textContent = "0 Orders";
    refs.optimisationCount.textContent = "0 Orders";
    refs.procurementCount.textContent = "0 Orders";
    refs.plannerCount.textContent = "0 Orders";
    refs.plannerMoveCount.textContent = "0 Orders";
    refs.productionCount.textContent = "0 Visible";
    refs.dispatchCount.textContent = "0 Visible";
    refs.sharedPlanningDataEntryCount.textContent = "0 Orders";
    refs.sharedPlanningOptimisationCount.textContent = "0 Orders";
    refs.sharedPlanningProcurementCount.textContent = "0 Orders";
    refs.sharedPlanningProductionCount.textContent = "0 Orders";
    refs.sharedPlanningDispatchCount.textContent = "0 Orders";
    refs.reportCount.textContent = "0 Rows";
    refs.auditCount.textContent = "0 Logs";
    refs.historyCount.textContent = "0 Rows";
    refs.weeklyRangeLabel.textContent = "Last 7 Days";
    refs.weeklyMetrics.innerHTML = "";
    refs.weeklyDailyBody.innerHTML = emptyRow(6, "Sign in to load weekly summary.");
    refs.weeklyModuleBody.innerHTML = emptyRow(4, "Sign in to load weekly summary.");
    refs.weeklyRecentBody.innerHTML = emptyRow(6, "Sign in to load weekly activity.");
    refs.weeklyModuleCount.textContent = "0 Modules";
    refs.weeklyRecentCount.textContent = "0 Logs";
    refs.currentBuildList.innerHTML = "";
    refs.pathBuildList.innerHTML = "";
    updateQuotationOrderNumberCounter();
    [
      refs.recentOrdersPagination,
      refs.dealerPagination,
      refs.quotationPagination,
      refs.optimisationPagination,
      refs.procurementPagination,
      refs.plannerPagination,
      refs.productionPagination,
      refs.dispatchPagination,
      refs.sharedPlanningDataEntryPagination,
      refs.sharedPlanningOptimisationPagination,
      refs.sharedPlanningProcurementPagination,
      refs.sharedPlanningProductionPagination,
      refs.sharedPlanningDispatchPagination,
      refs.reportsPagination,
      refs.auditPagination,
      refs.historyPagination,
      refs.usersPagination
    ].forEach((node) => {
      if (node) {
        node.innerHTML = "";
      }
    });
    closeHistoryModal();
    renderRoleVisibility();
    applySidebarLayout();
  }
  function renderAll() {
    refs.authShell.classList.add("hidden");
    refs.appShell.classList.remove("hidden");
    refs.authMessageStrip.classList.add("hidden");
    syncSidebarViewport();
    renderSession();
    renderRoleVisibility();
    renderMasterLookups();
    renderDataEntry();
    renderOptimisation();
    renderProcurement();
    renderPlanning();
    renderProduction();
    renderDispatch();
    renderReports();
    renderHistory();
    renderEmailLog();
    renderMasters();
    renderUsers();
    renderSettings();
    renderHistoryModal();
    updateQuotationOrderNumberCounter();
  }
  function updateQuotationOrderNumberCounter() {
    if (!refs.quotationOrderNumber || !refs.quotationOrderNumberCount) return;
    const max = Number(refs.quotationOrderNumber.dataset.counterMax || 200);
    const len = refs.quotationOrderNumber.value.length;
    refs.quotationOrderNumberCount.textContent = `${len} / ${max}`;
    refs.quotationOrderNumberCount.classList.toggle("is-over", len > max);
  }
  function renderSession() {
    refs.sessionRoleTag.textContent = state.session.role_name;
    refs.sessionName.textContent = state.session.full_name;
    refs.sessionMeta.textContent = `${state.session.role_name} | ${state.session.station_name}`;
  }
  function isMarketingUserSession() {
    var _a;
    return ((_a = state.session) == null ? void 0 : _a.role_name) === "Marketing User";
  }
  function currentMarketingOwnerValue() {
    var _a;
    return isMarketingUserSession() ? ((_a = state.session) == null ? void 0 : _a.full_name) || "" : refs.dealerMarketingOwner.value.trim();
  }
  function syncDealerMarketingOwnerField() {
    var _a;
    const marketingOwnerAddButton = document.querySelector('[data-quick-add-target="#dealer-marketing-owner"]');
    if (isMarketingUserSession()) {
      refs.dealerMarketingOwner.value = ((_a = state.session) == null ? void 0 : _a.full_name) || "";
      refs.dealerMarketingOwner.readOnly = true;
      marketingOwnerAddButton == null ? void 0 : marketingOwnerAddButton.classList.add("hidden");
      return;
    }
    if (refs.dealerMarketingOwner.readOnly) {
      refs.dealerMarketingOwner.value = "";
    }
    refs.dealerMarketingOwner.readOnly = false;
    marketingOwnerAddButton == null ? void 0 : marketingOwnerAddButton.classList.remove("hidden");
  }
  function renderRoleVisibility() {
    var _a, _b, _c, _d, _e;
    const allowedSections = state.session ? state.session.sections : [];
    document.querySelectorAll(".nav-link").forEach((button) => {
      button.classList.toggle("hidden", !allowedSections.includes(button.dataset.section));
    });
    const activeButton = document.querySelector(".nav-link.active");
    if (state.session && activeButton && !allowedSections.includes(activeButton.dataset.section)) {
      activateSection(state.session.home_section || "data-entry");
    }
    const isMarketingUser = isMarketingUserSession();
    (_a = refs.quotationEntryCard) == null ? void 0 : _a.classList.toggle("hidden", isMarketingUser);
    (_b = refs.confirmEntryCard) == null ? void 0 : _b.classList.toggle("hidden", isMarketingUser);
    (_c = refs.recentOrdersCard) == null ? void 0 : _c.classList.toggle("hidden", isMarketingUser);
    (_d = refs.quotationRegisterCard) == null ? void 0 : _d.classList.toggle("hidden", isMarketingUser);
    (_e = refs.sharedPlanningDataEntryCard) == null ? void 0 : _e.classList.toggle("hidden", isMarketingUser);
  }
  function renderMasterLookups() {
    if (!state.app) {
      return;
    }
    refs.customerTypeChipWrap.innerHTML = state.app.masters.customer_types.map((item) => `<span class="pill blue">${escapeHtml(item.code)}</span>`).join("");
    fillDatalist(refs.dealerOptions, state.app.data_entry.dealer_options);
    fillDatalist(refs.dealerTypeOptions, state.app.masters.dealer_types.map((item) => item.name));
    fillDatalist(refs.paymentTermsOptions, state.app.masters.payment_terms.map((item) => item.name));
    fillDatalist(refs.marketingOwnerOptions, state.app.masters.marketing_owners.map((item) => item.name));
    fillDatalist(refs.quotationOwnerOptions, state.app.masters.quotation_owners.map((item) => item.name));
    fillDatalist(refs.customerTypeOptions, state.app.masters.customer_types.map((item) => item.code));
    fillDatalist(refs.orderTypeOptions, state.app.masters.order_types.map((item) => item.name));
    fillDatalist(refs.orderClassOptions, state.app.masters.order_classes.map((item) => item.name));
    fillDatalist(refs.vendorOptions, state.app.masters.vendors.map((item) => item.name));
    fillDatalist(refs.confirmOrderOptions, state.app.data_entry.confirmable_orders);
    fillDatalist(refs.optimisationOrderOptions, state.app.optimisation.eligible_order_numbers);
    fillDatalist(refs.procurementOrderOptions, state.app.procurement.eligible_order_numbers);
    fillSelect(
      refs.procurementStatus,
      state.app.masters.procurement_statuses.map((item) => ({ value: item.code, label: item.label })),
      refs.procurementStatus.value || "PO_PENDING"
    );
    renderSequenceProfileControls();
    renderSharedPlanningControls();
    syncQuotationDealerFields();
    syncQuotationOrderClassFields();
    refreshDealerCodePreview();
  }
  function renderSharedPlanningControls() {
    if (!state.app) {
      return;
    }
    refs.sharedPlanningSearch.value = state.ui.sharedPlanningSearch;
    if (refs.sharedPlanningDataEntrySearch) {
      refs.sharedPlanningDataEntrySearch.value = state.ui.sharedPlanningSearch;
    }
    const stageOptions = [{ value: "all", label: "All Stages" }];
    uniqueValues((state.app.planning.rows || []).map((row) => row.planner_stage_label)).forEach((value) => {
      stageOptions.push({ value, label: value });
    });
    fillSelect(refs.sharedPlanningStage, stageOptions, state.ui.sharedPlanningStage);
    refs.sharedPlanningSort.value = state.ui.sharedPlanningSort;
  }
  function renderSequenceProfileControls() {
    var _a, _b, _c, _d, _e, _f;
    if (!state.app) {
      return;
    }
    const orderTypes = state.app.masters.order_types || [];
    const orderClasses = state.app.masters.order_classes || [];
    const sequenceProfiles = state.app.masters.sequence_profiles || [];
    fillSelect(refs.plannerSequenceOrderType, orderTypes.map((item) => ({ value: item.id, label: item.name })), refs.plannerSequenceOrderType.value || String(((_a = orderTypes[0]) == null ? void 0 : _a.id) || ""));
    fillSelect(refs.plannerSequenceOrderClass, orderClasses.map((item) => ({ value: item.name, label: item.name })), refs.plannerSequenceOrderClass.value || ((_b = orderClasses[0]) == null ? void 0 : _b.name) || "");
    fillSelect(refs.plannerSequenceStation, (state.app.masters.machines || []).map((item) => ({ value: item.id, label: item.name })), refs.plannerSequenceStation.value || String(((_d = (_c = state.app.masters.machines) == null ? void 0 : _c[0]) == null ? void 0 : _d.id) || ""));
    const selectedProfileId = state.ui.selectedSequenceProfileId || String(((_e = sequenceProfiles[0]) == null ? void 0 : _e.id) || "");
    if (!sequenceProfiles.some((item) => String(item.id) === selectedProfileId)) {
      state.ui.selectedSequenceProfileId = String(((_f = sequenceProfiles[0]) == null ? void 0 : _f.id) || "");
    }
    fillSelect(
      refs.plannerProfileSelect,
      sequenceProfiles.map((item) => ({
        value: item.id,
        label: `${item.name} | ${item.order_type_name} | ${item.order_class}`
      })),
      state.ui.selectedSequenceProfileId
    );
  }
  function plannerColumnMap() {
    return Object.fromEntries(PLANNER_COLUMNS.map((column) => [column.id, column]));
  }
  function plannerColumnStorageKey() {
    var _a;
    const loginId = String(((_a = state.session) == null ? void 0 : _a.login_id) || "guest").toLowerCase();
    return `elenzaPlannerColumns:${loginId}`;
  }
  function normalizePlannerColumnOrder(order) {
    const validIds = PLANNER_COLUMNS.map((column) => column.id);
    const provided = Array.isArray(order) ? order.filter((id) => validIds.includes(id)) : [];
    const missing = validIds.filter((id) => !provided.includes(id));
    return [...provided, ...missing];
  }
  function ensurePlannerColumnOrder() {
    var _a;
    const loginId = String(((_a = state.session) == null ? void 0 : _a.login_id) || "").toLowerCase();
    if (state.ui.plannerColumnLogin === loginId && state.ui.plannerColumnOrder.length) {
      state.ui.plannerColumnOrder = normalizePlannerColumnOrder(state.ui.plannerColumnOrder);
      return;
    }
    try {
      const raw = localStorage.getItem(plannerColumnStorageKey());
      state.ui.plannerColumnOrder = normalizePlannerColumnOrder(raw ? JSON.parse(raw) : []);
      state.ui.plannerColumnLogin = loginId;
    } catch (e) {
      state.ui.plannerColumnOrder = normalizePlannerColumnOrder([]);
      state.ui.plannerColumnLogin = loginId;
    }
  }
  function savePlannerColumnOrder() {
    try {
      localStorage.setItem(plannerColumnStorageKey(), JSON.stringify(normalizePlannerColumnOrder(state.ui.plannerColumnOrder)));
    } catch (e) {
    }
  }
  function resetPlannerColumnOrder() {
    var _a;
    state.ui.plannerColumnOrder = normalizePlannerColumnOrder([]);
    state.ui.plannerColumnLogin = String(((_a = state.session) == null ? void 0 : _a.login_id) || "").toLowerCase();
    savePlannerColumnOrder();
    renderPlanning();
    showMessage("Planner columns reset.", "success");
  }
  function plannerHeaderFilterOptions(columnId) {
    var _a, _b;
    const rows = ((_b = (_a = state.app) == null ? void 0 : _a.planning) == null ? void 0 : _b.rows) || [];
    switch (columnId) {
      case "customer_type":
        return uniqueValues(rows.map((row) => row.customer_type || ""));
      case "order_type":
        return uniqueValues(rows.map((row) => row.order_type || ""));
      case "order_class":
        return uniqueValues(rows.map((row) => row.order_class || ""));
      case "current_status":
        return uniqueValues(rows.map((row) => row.current_stage_hint || row.planner_stage_label || ""));
      default:
        return [];
    }
  }
  function renderPlannerHeaderFilter(columnId) {
    const value = String(state.ui.plannerColumnFilters[columnId] || "");
    if (["order_number", "customer_name", "priority"].includes(columnId)) {
      return `<input class="planner-head-filter-input" type="search" data-planner-filter="${columnId}" value="${escapeAttribute(value)}" placeholder="Filter">`;
    }
    if (["customer_type", "order_type", "order_class", "current_status"].includes(columnId)) {
      const options = [`<option value="">All</option>`].concat(plannerHeaderFilterOptions(columnId).map((item) => `<option value="${escapeAttribute(item)}" ${item === value ? "selected" : ""}>${escapeHtml(item || "-")}</option>`));
      return `<select class="planner-head-filter-input" data-planner-filter="${columnId}">${options.join("")}</select>`;
    }
    return `<span class="planner-head-filter-spacer"></span>`;
  }
  function renderPlannerHeader() {
    if (!refs.plannerHeadRow) {
      return;
    }
    ensurePlannerColumnOrder();
    const columnLookup = plannerColumnMap();
    const headerCells = state.ui.plannerColumnOrder.map((columnId) => {
      const column = columnLookup[columnId];
      const isSorted = state.ui.plannerSort === column.id;
      const sortArrow = isSorted ? state.ui.plannerSortDir === "desc" ? " ?" : " ?" : "";
      return `<th draggable="true" data-planner-column="${column.id}" class="planner-column-head"><button class="planner-head-button" type="button" data-planner-sort="${column.id}">${escapeHtml(column.label)}${sortArrow}</button></th>`;
    }).join("");
    const filterCells = state.ui.plannerColumnOrder.map((columnId) => {
      return `<th class="planner-filter-head">${renderPlannerHeaderFilter(columnId)}</th>`;
    }).join("");
    refs.plannerHeadRow.innerHTML = `<tr>${headerCells}</tr><tr class="planner-filter-row">${filterCells}</tr>`;
  }
  function formatPlannerDateDisplay(value) {
    const normalized = String(value || "").trim();
    if (!normalized) {
      return "";
    }
    const parsed = /* @__PURE__ */ new Date(`${normalized}T00:00:00`);
    if (Number.isNaN(parsed.getTime())) {
      return normalized;
    }
    const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    return `${String(parsed.getDate()).padStart(2, "0")}-${months[parsed.getMonth()]}-${String(parsed.getFullYear()).slice(-2)}`;
  }
  function normalizePlannerDateInput(value) {
    const raw = String(value || "").trim();
    if (!raw) {
      return "";
    }
    if (/^\d{4}-\d{2}-\d{2}$/.test(raw)) {
      return raw;
    }
    const match = raw.match(/^(\d{1,2})[-\/ ]([A-Za-z]{3})[-\/ ](\d{2}|\d{4})$/);
    if (!match) {
      return raw;
    }
    const day = String(match[1]).padStart(2, "0");
    const monthMap = { jan: "01", feb: "02", mar: "03", apr: "04", may: "05", jun: "06", jul: "07", aug: "08", sep: "09", oct: "10", nov: "11", dec: "12" };
    const month = monthMap[String(match[2]).toLowerCase()];
    if (!month) {
      return raw;
    }
    const yearRaw = String(match[3]);
    const year = yearRaw.length === 2 ? `20${yearRaw}` : yearRaw;
    return `${year}-${month}-${day}`;
  }
  function renderPlannerCell(row, columnId, canEdit) {
    const statusText = escapeHtml(row.current_stage_hint || row.planner_stage_label || "-");
    switch (columnId) {
      case "confirmation_date":
        return escapeHtml(formatPlannerDateDisplay(row.confirmation_date || "") || "-");
      case "order_number":
        return `<div class="planner-order-cell"><button class="planner-order-link" type="button" data-open-history="${row.order_id}" title="Open history">${escapeHtml(row.order_number)}</button><span class="planner-meta">#${escapeHtml(row.planning_rank)}</span>${canEdit ? `<div class="planner-inline-actions"><button class="micro-button planner-step-button" type="button" data-planner-move="up" data-order-id="${row.order_id}" title="Move up">?</button><button class="micro-button planner-step-button" type="button" data-planner-move="down" data-order-id="${row.order_id}" title="Move down">?</button></div>` : ""}</div>`;
      case "customer_name":
        return escapeHtml(customerLabel(row.customer_name));
      case "customer_type":
        return escapeHtml(row.customer_type || "-");
      case "order_type":
        return escapeHtml(row.order_type || "-");
      case "order_class":
        return escapeHtml(row.order_class || "-");
      case "material_received_date":
        return escapeHtml(formatPlannerDateDisplay(row.material_received_date || "") || "-");
      case "current_status":
        return `<div class="planner-status-cell"><span class="planner-status-text">${statusText}</span>${row.partial_pending ? '<span class="planner-status-note">Partially Pending</span>' : ""}</div>`;
      case "edd":
        return canEdit ? `<input class="planner-mini-input planner-sla-input" type="text" id="planner-sla-edit-${row.order_id}" value="${escapeAttribute(formatPlannerDateDisplay(row.edd || ""))}" placeholder="dd-mmm-yy">` : escapeHtml(formatPlannerDateDisplay(row.edd || "") || "-");
      case "panel_qty":
        return escapeHtml(row.panel_qty || "-");
      case "board_qty":
        return escapeHtml(row.board_qty || "-");
      case "priority":
        return `<div class="planner-priority-cell">${canEdit ? `<select class="planner-mini-input planner-short-input" id="planner-priority-edit-${row.order_id}"><option value="" ${!row.priority ? "selected" : ""}>-</option><option value="High" ${row.priority === "High" ? "selected" : ""}>High</option><option value="Medium" ${row.priority === "Medium" ? "selected" : ""}>Medium</option><option value="Low" ${row.priority === "Low" ? "selected" : ""}>Low</option></select>` : escapeHtml(row.priority || "-")}</div>`;
      default:
        return "-";
    }
  }
  function syncPlannerFilters() {
    var _a, _b;
    state.ui.plannerSearch = ((_a = refs.plannerSearch) == null ? void 0 : _a.value.trim()) || "";
    state.ui.plannerStage = ((_b = refs.plannerStageFilter) == null ? void 0 : _b.value) || "all";
    state.ui.pagination.planner = 1;
    state.ui.pagination.plannerMove = 1;
    renderPlanning();
  }
  function getPlannerRows() {
    var _a, _b;
    let rows = [...((_b = (_a = state.app) == null ? void 0 : _a.planning) == null ? void 0 : _b.rows) || []].filter((row) => !String(row.dispatch_status || row.current_stage_hint || "").toLowerCase().includes("dispatched"));
    const search = state.ui.plannerSearch.trim().toLowerCase();
    if (search) {
      rows = rows.filter((row) => [
        row.order_number,
        row.customer_name,
        row.customer_type,
        row.order_type,
        row.order_class,
        row.current_stage_hint,
        row.priority,
        row.visible_stations
      ].join(" ").toLowerCase().includes(search));
    }
    if (state.ui.plannerStage && state.ui.plannerStage !== "all") {
      rows = rows.filter((row) => row.current_stage_hint === state.ui.plannerStage || row.planner_stage_label === state.ui.plannerStage);
    }
    Object.entries(state.ui.plannerColumnFilters || {}).forEach(([columnId, filterValue]) => {
      const active = String(filterValue || "").trim().toLowerCase();
      if (!active) return;
      rows = rows.filter((row) => {
        const valueMap = {
          order_number: row.order_number,
          customer_name: customerLabel(row.customer_name),
          customer_type: row.customer_type,
          order_type: row.order_type,
          order_class: row.order_class,
          current_status: row.current_stage_hint || row.planner_stage_label,
          priority: row.priority
        };
        const raw = String(valueMap[columnId] || "").toLowerCase();
        return ["customer_type", "order_type", "order_class", "current_status"].includes(columnId) ? raw === active : raw.includes(active);
      });
    });
    switch (state.ui.plannerSort) {
      case "confirmation_date":
        rows.sort((a, b) => String(a.confirmation_date || "").localeCompare(String(b.confirmation_date || "")) * (state.ui.plannerSortDir === "desc" ? -1 : 1));
        break;
      case "material_received_date":
        rows.sort((a, b) => String(a.material_received_date || "").localeCompare(String(b.material_received_date || "")) * (state.ui.plannerSortDir === "desc" ? -1 : 1));
        break;
      case "edd":
        rows.sort((a, b) => String(a.edd || "9999-12-31").localeCompare(String(b.edd || "9999-12-31")) * (state.ui.plannerSortDir === "desc" ? -1 : 1));
        break;
      case "order_number":
        rows.sort((a, b) => String(a.order_number || "").localeCompare(String(b.order_number || "")) * (state.ui.plannerSortDir === "desc" ? -1 : 1));
        break;
      case "customer_name":
        rows.sort((a, b) => String(customerLabel(a.customer_name) || "").localeCompare(String(customerLabel(b.customer_name) || "")) * (state.ui.plannerSortDir === "desc" ? -1 : 1));
        break;
      case "customer_type":
        rows.sort((a, b) => String(a.customer_type || "").localeCompare(String(b.customer_type || "")) * (state.ui.plannerSortDir === "desc" ? -1 : 1));
        break;
      case "order_type":
        rows.sort((a, b) => String(a.order_type || "").localeCompare(String(b.order_type || "")) * (state.ui.plannerSortDir === "desc" ? -1 : 1));
        break;
      case "order_class":
        rows.sort((a, b) => String(a.order_class || "").localeCompare(String(b.order_class || "")) * (state.ui.plannerSortDir === "desc" ? -1 : 1));
        break;
      case "current_status":
        rows.sort((a, b) => String(a.current_stage_hint || a.planner_stage_label || "").localeCompare(String(b.current_stage_hint || b.planner_stage_label || "")) * (state.ui.plannerSortDir === "desc" ? -1 : 1));
        break;
      case "panel_qty":
        rows.sort((a, b) => (Number(a.panel_qty || 0) - Number(b.panel_qty || 0)) * (state.ui.plannerSortDir === "desc" ? -1 : 1));
        break;
      case "board_qty":
        rows.sort((a, b) => (Number(a.board_qty || 0) - Number(b.board_qty || 0)) * (state.ui.plannerSortDir === "desc" ? -1 : 1));
        break;
      case "priority":
        rows.sort((a, b) => String(a.priority || "").localeCompare(String(b.priority || "")) * (state.ui.plannerSortDir === "desc" ? -1 : 1));
        break;
      case "rank-asc":
      default:
        rows.sort((a, b) => Number(a.planning_rank || 0) - Number(b.planning_rank || 0));
        break;
    }
    return rows;
  }
  function getSharedPlanningRows() {
    var _a, _b;
    let rows = [...((_b = (_a = state.app) == null ? void 0 : _a.planning) == null ? void 0 : _b.rows) || []];
    const search = state.ui.sharedPlanningSearch.trim().toLowerCase();
    const stage = state.ui.sharedPlanningStage;
    if (search) {
      rows = rows.filter((row) => {
        const haystack = [
          row.order_number,
          row.dealer_name,
          row.customer_name,
          row.visible_stations,
          row.priority,
          row.urgency,
          row.planner_remarks
        ].join(" ").toLowerCase();
        return haystack.includes(search);
      });
    }
    if (stage && stage !== "all") {
      rows = rows.filter((row) => row.planner_stage_label === stage);
    }
    return sortSharedPlanningRows(rows);
  }
  function sortSharedPlanningRows(rows) {
    const list = [...rows];
    switch (state.ui.sharedPlanningSort) {
      case "rank-desc":
        return list.sort((a, b) => Number(b.planning_rank || 0) - Number(a.planning_rank || 0));
      case "sla-asc":
        return list.sort((a, b) => String(a.sla_date || "9999-12-31").localeCompare(String(b.sla_date || "9999-12-31")));
      case "order-asc":
        return list.sort((a, b) => String(a.order_number || "").localeCompare(String(b.order_number || "")));
      case "dealer-asc":
        return list.sort((a, b) => String(a.dealer_name || "").localeCompare(String(b.dealer_name || "")));
      case "priority-asc":
        return list.sort((a, b) => String(a.priority || "").localeCompare(String(b.priority || "")));
      default:
        return list.sort((a, b) => Number(a.planning_rank || 0) - Number(b.planning_rank || 0));
    }
  }
  function matchesSearch(values, search) {
    const needle = String(search || "").trim().toLowerCase();
    if (!needle) {
      return true;
    }
    return values.some((value) => String(value || "").toLowerCase().includes(needle));
  }
  function filterPlanningRowsBySearch(rows, search) {
    return rows.filter((row) => matchesSearch([
      row.order_number,
      row.dealer_name,
      row.customer_name,
      row.current_stage_hint,
      row.priority,
      row.urgency,
      row.visible_stations,
      row.planner_remarks
    ], search));
  }
  function tablePageSize(key) {
    const sizes = {
      recentOrders: 6,
      dealerRegister: 6,
      quotationRegister: 6,
      optimisation: 50,
      procurement: 8,
      planner: 50,
      plannerMove: 50,
      production: 5,
      dispatch: 5,
      sharedPlanningDataEntry: 6,
      sharedPlanningOptimisation: 6,
      sharedPlanningProcurement: 6,
      sharedPlanningProduction: 6,
      sharedPlanningDispatch: 6,
      reports: 8,
      audit: 8,
      history: 8,
      users: 8,
      emailLog: 8,
      machineReport: 8
    };
    return sizes[key] || 8;
  }
  function getPaginationSlice(key, rows) {
    const pageSize = tablePageSize(key);
    const total = rows.length;
    const totalPages = Math.max(1, Math.ceil(total / pageSize));
    const current = Math.min(state.ui.pagination[key] || 1, totalPages);
    state.ui.pagination[key] = current;
    const startIndex = (current - 1) * pageSize;
    const endIndex = Math.min(startIndex + pageSize, total);
    return {
      rows: rows.slice(startIndex, endIndex),
      pageSize,
      total,
      totalPages,
      current,
      start: total ? startIndex + 1 : 0,
      end: endIndex
    };
  }
  function renderPagination(target, key, meta) {
    if (!target) {
      return;
    }
    if (!meta.total) {
      target.innerHTML = '<span class="page-status">0 rows</span>';
      return;
    }
    target.innerHTML = `
    <button class="page-button" type="button" data-page-key="${key}" data-page-direction="-1" ${meta.current <= 1 ? "disabled" : ""}>Prev</button>
    <span class="page-status">${meta.start}-${meta.end} of ${meta.total}</span>
    <button class="page-button" type="button" data-page-key="${key}" data-page-direction="1" ${meta.current >= meta.totalPages ? "disabled" : ""}>Next</button>
  `;
  }
  function renderPagedTable(config) {
    const meta = getPaginationSlice(config.key, config.rows);
    config.bodyRef.innerHTML = meta.total ? meta.rows.map(config.renderRow).join("") : emptyRow(config.emptyColumns, config.emptyMessage);
    renderPagination(config.paginationRef, config.key, meta);
    return meta;
  }
  function renderDataEntry() {
    var _a, _b, _c, _d;
    if (!state.app) {
      return;
    }
    const dataEntry = state.app.data_entry;
    syncDealerMarketingOwnerField();
    ensureConfirmDateTimeDefault();
    const recentOrdersSearch = (((_a = refs.recentOrdersSearch) == null ? void 0 : _a.value) || "").trim().toLowerCase();
    const recentOrdersRows = recentOrdersSearch ? dataEntry.recent_orders.filter((order) => matchesSearch([
      order.order_number,
      order.dealer_name,
      order.customer_name,
      order.workflow_stage,
      order.procurement_status,
      order.visible_stations
    ], recentOrdersSearch)) : dataEntry.recent_orders;
    renderPagedTable({
      key: "recentOrders",
      rows: recentOrdersRows,
      bodyRef: refs.recentOrdersBody,
      paginationRef: refs.recentOrdersPagination,
      emptyColumns: 6,
      emptyMessage: "No orders yet.",
      renderRow: (order) => `
        <tr>
          <td>${escapeHtml(order.order_number)}</td>
          <td>${escapeHtml(order.dealer_name)}</td>
          <td>${escapeHtml(customerLabel(order.customer_name))}</td>
          <td>${pill(order.workflow_stage)}</td>
          <td>${escapeHtml(order.procurement_status)}</td>
          <td>${escapeHtml(order.visible_stations)}</td>
        </tr>
      `
    });
    const dealerSearch = (((_b = refs.dealerRegisterSearch) == null ? void 0 : _b.value) || "").trim().toLowerCase();
    const quotationSearch = (((_c = refs.quotationRegisterSearch) == null ? void 0 : _c.value) || "").trim().toLowerCase();
    const dealerRows = dealerSearch ? dataEntry.dealers.filter((dealer) => [
      dealer.dealer_code,
      dealer.dealer_name,
      dealer.company_name,
      dealer.dealer_type,
      dealer.customer_type_code,
      dealer.contact_person,
      dealer.mobile_number,
      dealer.city,
      dealer.gst_number
    ].some((value) => String(value || "").toLowerCase().includes(dealerSearch))) : dataEntry.dealers;
    const canManageDealers = ((_d = state.session) == null ? void 0 : _d.role_name) === "Admin";
    renderPagedTable({
      key: "dealerRegister",
      rows: dealerRows,
      bodyRef: refs.dealerRegisterBody,
      paginationRef: refs.dealerPagination,
      emptyColumns: 10,
      emptyMessage: "No dealers yet.",
      renderRow: (dealer) => `
        <tr>
          <td>${escapeHtml(dealer.dealer_code || "-")}</td>
          <td>${escapeHtml(dealer.dealer_name)}</td>
          <td>${escapeHtml(dealer.company_name || "-")}</td>
          <td>${escapeHtml(dealer.dealer_type || "-")}</td>
          <td>${escapeHtml(dealer.customer_type_code || "-")}</td>
          <td>${escapeHtml(dealer.contact_person || "-")}</td>
          <td>${escapeHtml(dealer.mobile_number || "-")}</td>
          <td>${escapeHtml(dealer.city || "-")}</td>
          <td>${escapeHtml(dealer.gst_number || "-")}</td>
          <td>${canManageDealers ? `<button class="micro-button" type="button" data-dealer-edit="${dealer.dealer_id}">Edit</button> <button class="micro-button danger" type="button" data-dealer-delete="${dealer.dealer_id}">Del</button>` : "-"}</td>
        </tr>
      `
    });
    const quotationRows = quotationSearch ? dataEntry.quotations.filter((order) => [
      order.quotation_date,
      order.quotation_number,
      order.order_number,
      order.dealer_name,
      order.customer_name,
      order.order_type,
      order.workflow_stage,
      order.main_order,
      order.sub_order,
      order.customer_type_code
    ].some((value) => String(value || "").toLowerCase().includes(quotationSearch))) : dataEntry.quotations;
    renderPagedTable({
      key: "quotationRegister",
      rows: quotationRows,
      bodyRef: refs.quotationRegisterBody,
      paginationRef: refs.quotationPagination,
      emptyColumns: 8,
      emptyMessage: "No quotations yet.",
      renderRow: (order) => {
        const canDelete = canDeleteQuotation(order);
        return `
        <tr class="accordion-row" data-quotation-row="${order.order_id}">
          <td>${escapeHtml(order.quotation_date || "-")}</td>
          <td>${escapeHtml(order.quotation_number)}</td>
          <td>${escapeHtml(order.order_number)}</td>
          <td>${escapeHtml(order.dealer_name)}</td>
          <td>${escapeHtml(customerLabel(order.customer_name))}</td>
          <td>${escapeHtml(order.order_type)}</td>
          <td>${pill(order.workflow_stage)}</td>
          <td>
            <button class="micro-button" type="button" data-quotation-accordion="${order.order_id}">Delete / More</button>
          </td>
        </tr>
        <tr class="accordion-panel hidden" data-quotation-panel="${order.order_id}">
          <td colspan="8">
            <div class="accordion-card">
              <div class="accordion-card__title">Quotation Actions</div>
              <div class="accordion-card__body">
                <button class="micro-button danger" type="button" data-quotation-delete="${order.order_id}" ${canDelete ? "" : "disabled"}>Delete quotation</button>
                <span class="accordion-note">Delete only allowed for quotation-created rows.</span>
              </div>
            </div>
          </td>
        </tr>`;
      }
    });
    if (refs.quotationCount) {
      refs.quotationCount.textContent = `${quotationRows.length} Quotations`;
    }
    refs.dealerCount.textContent = `${dealerRows.length} Dealers`;
    refs.quotationCount.textContent = `${dataEntry.quotations.length} Quotations`;
    refs.recentOrdersCount.textContent = `${recentOrdersRows.length} Orders`;
    renderSharedPlanningTable("sharedPlanningDataEntry", refs.sharedPlanningDataEntryBody, refs.sharedPlanningDataEntryPagination, refs.sharedPlanningDataEntryCount);
  }
  function indiaNowDateTimeLocalValue() {
    const parts = new Intl.DateTimeFormat("sv-SE", {
      timeZone: "Asia/Kolkata",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      hour12: false
    }).formatToParts(/* @__PURE__ */ new Date()).reduce((acc, part) => {
      if (part.type !== "literal") {
        acc[part.type] = part.value;
      }
      return acc;
    }, {});
    return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}`;
  }
  function indiaTodayDateValue() {
    const parts = new Intl.DateTimeFormat("sv-SE", {
      timeZone: "Asia/Kolkata",
      year: "numeric",
      month: "2-digit",
      day: "2-digit"
    }).formatToParts(/* @__PURE__ */ new Date()).reduce((acc, part) => {
      if (part.type !== "literal") {
        acc[part.type] = part.value;
      }
      return acc;
    }, {});
    return `${parts.year}-${parts.month}-${parts.day}`;
  }
  function ensureConfirmDateTimeDefault(force = false) {
    if (!refs.confirmDateTime) {
      return;
    }
    if (force || !refs.confirmDateTime.value) {
      refs.confirmDateTime.value = indiaNowDateTimeLocalValue();
    }
  }
  function ensureOptimisationDateTimeDefault(force = false) {
    if (!refs.optimisationDateTime) {
      return;
    }
    if (force || !refs.optimisationDateTime.value) {
      refs.optimisationDateTime.value = indiaTodayDateValue();
    }
  }
  function ensureOptimisationDefaults(force = false) {
    if (refs.optimisationBoards && (force || !refs.optimisationBoards.value)) {
      refs.optimisationBoards.value = "1";
    }
    if (refs.optimisationPanels && (force || !refs.optimisationPanels.value)) {
      refs.optimisationPanels.value = "1";
    }
    if (refs.optimisationRmDetails && (force || !refs.optimisationRmDetails.value)) {
      refs.optimisationRmDetails.value = "1";
    }
  }
  function ensureProcurementDateDefaults(force = false) {
    if (refs.procurementPoDate && (force || !refs.procurementPoDate.value)) {
      refs.procurementPoDate.value = indiaTodayDateValue();
    }
    if (refs.procurementMrnDate && (force || !refs.procurementMrnDate.value)) {
      refs.procurementMrnDate.value = indiaTodayDateValue();
    }
  }
  function ensureProcurementDefaults(force = false) {
    if (refs.procurementPoNumber && (force || !refs.procurementPoNumber.value)) {
      refs.procurementPoNumber.value = "1";
    }
    if (refs.procurementVendor && (force || !refs.procurementVendor.value)) {
      refs.procurementVendor.value = "1";
    }
    if (refs.procurementItemDetails && (force || !refs.procurementItemDetails.value)) {
      refs.procurementItemDetails.value = "1";
    }
    if (refs.procurementRemarks && (force || !refs.procurementRemarks.value)) {
      refs.procurementRemarks.value = "1";
    }
  }
  function renderOptimisation() {
    var _a;
    if (!state.app) {
      return;
    }
    const rows = getOptimisationRows();
    const hasOrders = rows.length > 0;
    ensureOptimisationDateTimeDefault();
    ensureOptimisationDefaults();
    renderPagedTable({
      key: "optimisation",
      rows,
      bodyRef: refs.optimisationBody,
      paginationRef: refs.optimisationPagination,
      pageSize: 50,
      emptyColumns: 5,
      emptyMessage: "No orders are waiting for optimisation.",
      renderRow: (row) => `
        <tr>
          <td>${escapeHtml(row.confirmation_date || "-")}</td>
          <td>${escapeHtml(row.order_number)}</td>
          <td>${escapeHtml(row.dealer_name)}</td>
          <td>${escapeHtml(customerLabel(row.customer_name))}</td>
          <td>${escapeHtml(row.order_type)}</td>
        </tr>
      `
    });
    refs.optimisationCount.textContent = `${rows.length} Orders`;
    setFormDisabled(refs.optimisationForm, !hasOrders, "No orders are waiting for optimisation.");
    if (!hasOrders) {
      refs.optimisationForm.reset();
      ensureOptimisationDateTimeDefault(true);
      ensureOptimisationDefaults(true);
    }
    renderSharedPlanningTable("sharedPlanningOptimisation", refs.sharedPlanningOptimisationBody, refs.sharedPlanningOptimisationPagination, refs.sharedPlanningOptimisationCount, ((_a = refs.sharedPlanningOptimisationSearch) == null ? void 0 : _a.value) || "");
  }
  function getOptimisationRows() {
    var _a;
    if (!state.app) {
      return [];
    }
    const search = (((_a = refs.optimisationSearch) == null ? void 0 : _a.value) || "").trim().toLowerCase();
    return search ? state.app.optimisation.rows.filter((row) => [
      row.confirmation_date,
      row.order_number,
      row.dealer_name,
      row.customer_name,
      row.order_type
    ].some((value) => String(value || "").toLowerCase().includes(search))) : state.app.optimisation.rows;
  }
  function renderProcurement() {
    var _a, _b;
    if (!state.app) {
      return;
    }
    const procurementSearch = (((_a = refs.procurementSearch) == null ? void 0 : _a.value) || "").trim().toLowerCase();
    const rows = procurementSearch ? (state.app.procurement.rows || []).filter((row) => matchesSearch([
      row.order_number,
      row.dealer_name,
      row.customer_name,
      row.procurement_status,
      row.updated_at
    ], procurementSearch)) : state.app.procurement.rows;
    const hasOrders = rows.length > 0;
    ensureProcurementDateDefaults();
    ensureProcurementDefaults();
    renderPagedTable({
      key: "procurement",
      rows,
      bodyRef: refs.procurementBody,
      paginationRef: refs.procurementPagination,
      emptyColumns: 5,
      emptyMessage: "No orders are waiting for procurement.",
      renderRow: (row) => `
        <tr>
          <td>${escapeHtml(row.order_number)}</td>
          <td>${escapeHtml(row.dealer_name)}</td>
          <td>${escapeHtml(customerLabel(row.customer_name))}</td>
          <td>${escapeHtml(row.procurement_status)}</td>
          <td>${escapeHtml(row.updated_at)}</td>
        </tr>
      `
    });
    refs.procurementCount.textContent = `${rows.length} Orders`;
    setFormDisabled(refs.procurementForm, !hasOrders, "No orders are waiting for procurement.");
    if (!hasOrders) {
      refs.procurementForm.reset();
      ensureProcurementDateDefaults(true);
      ensureProcurementDefaults(true);
    }
    renderSharedPlanningTable("sharedPlanningProcurement", refs.sharedPlanningProcurementBody, refs.sharedPlanningProcurementPagination, refs.sharedPlanningProcurementCount, ((_b = refs.sharedPlanningProcurementSearch) == null ? void 0 : _b.value) || "");
  }
  function renderPlanning() {
    if (!state.app) {
      return;
    }
    ensurePlannerColumnOrder();
    renderPlannerHeader();
    const allRows = state.app.planning.rows || [];
    const rows = getPlannerRows();
    fillSelect(refs.plannerStageFilter, ["all"].concat(uniqueValues(allRows.map((row) => row.current_stage_hint || row.planner_stage_label))).map((value) => ({ value, label: value === "all" ? "All" : value })), state.ui.plannerStage);
    if (refs.plannerSearch) refs.plannerSearch.value = state.ui.plannerSearch;
    renderPagedTable({
      key: "planner",
      rows,
      bodyRef: refs.plannerBody,
      paginationRef: refs.plannerPagination,
      emptyColumns: 12,
      emptyMessage: "No production planning rows available.",
      renderRow: (row) => renderPlannerQueueRow(row, state.app.planning.can_edit)
    });
    refs.plannerCount.textContent = `${rows.length} Orders`;
    renderPlannerMovePage(rows);
    renderPlannerSubtab();
    renderPlannerSequenceList();
    renderSharedPlanningTable("sharedPlanningOptimisation", refs.sharedPlanningOptimisationBody, refs.sharedPlanningOptimisationPagination, refs.sharedPlanningOptimisationCount);
  }
  function renderPlannerMovePage(rows) {
    var _a, _b;
    const machineOptions = (((_b = (_a = state.app) == null ? void 0 : _a.masters) == null ? void 0 : _b.machines) || []).map((item) => `<option value="${escapeAttribute(item.name)}">${escapeHtml(item.name)}</option>`).join("");
    renderPagedTable({
      key: "plannerMove",
      rows,
      bodyRef: refs.plannerMoveBody,
      paginationRef: refs.plannerMovePagination,
      emptyColumns: 6,
      emptyMessage: "No movable planning rows available.",
      renderRow: (row) => `
      <tr>
        <td><button class="planner-order-link" type="button" data-open-history="${row.order_id}">${escapeHtml(row.order_number)}</button></td>
        <td>${escapeHtml(row.dealer_name || "-")}</td>
        <td>${escapeHtml(customerLabel(row.customer_name))}</td>
        <td>${escapeHtml(row.assigned_station || row.current_stage_hint || "-")}</td>
        <td>
          <select class="planner-mini-input planner-station-select" id="planner-station-edit-${row.order_id}">
            ${machineOptions}
          </select>
        </td>
        <td><button class="micro-button planner-tick-button" type="button" data-planner-assign-station="${row.order_id}">Tick</button></td>
      </tr>
    `
    });
    rows.forEach((row) => {
      const ref = document.querySelector(`#planner-station-edit-${row.order_id}`);
      if (ref) {
        ref.value = row.assigned_station || "";
      }
    });
    refs.plannerMoveCount.textContent = `${rows.length} Orders`;
  }
  function renderPlannerSubtab() {
    var _a, _b, _c, _d;
    const current = state.ui.plannerSubtab || "queue";
    document.querySelectorAll("[data-planner-subtab]").forEach((button) => {
      button.classList.toggle("active", button.dataset.plannerSubtab === current);
    });
    (_a = refs.plannerFieldToolbar) == null ? void 0 : _a.classList.toggle("hidden", current === "sequence");
    (_b = refs.plannerQueuePanel) == null ? void 0 : _b.classList.toggle("hidden", current !== "queue");
    (_c = refs.plannerMovePanel) == null ? void 0 : _c.classList.toggle("hidden", current !== "move");
    (_d = refs.plannerSequencePanel) == null ? void 0 : _d.classList.toggle("hidden", current !== "sequence");
  }
  function renderPlannerSequenceList() {
    var _a, _b, _c, _d;
    const profiles = ((_b = (_a = state.app) == null ? void 0 : _a.masters) == null ? void 0 : _b.sequence_profiles) || [];
    const selected = profiles.find((item) => String(item.id) === String(state.ui.selectedSequenceProfileId || refs.plannerProfileSelect.value));
    if (!selected) {
      refs.plannerMachineSequenceList.innerHTML = '<div class="detail-card">No custom sequence profile available.</div>';
      return;
    }
    const machineOptions = (((_d = (_c = state.app) == null ? void 0 : _c.masters) == null ? void 0 : _d.machines) || []).map((machine) => {
      return (selectedStationId) => `<option value="${machine.id}" ${String(machine.id) === String(selectedStationId) ? "selected" : ""}>${escapeHtml(machine.name)}</option>`;
    });
    refs.plannerMachineSequenceList.innerHTML = selected.stations.length ? selected.stations.map((item) => `
      <div class="list-row">
        <div class="sequence-edit-row">
          <span class="list-label">${escapeHtml(item.sequence_no)}.</span>
          <select class="planner-mini-input sequence-station-picker" id="sequence-station-${item.id}">
            ${machineOptions.map((build) => build(item.station_id)).join("")}
          </select>
        </div>
        <div class="list-actions">
          <button class="micro-button" type="button" data-sequence-station-save="${item.id}">Save</button>
          <button class="micro-button" type="button" data-sequence-station-direction="up" data-sequence-station-id="${item.id}">Up</button>
          <button class="micro-button" type="button" data-sequence-station-direction="down" data-sequence-station-id="${item.id}">Dn</button>
          <button class="micro-button" type="button" data-sequence-station-delete="${item.id}">X</button>
        </div>
      </div>
    `).join("") : '<div class="detail-card">No stations mapped in this sequence yet.</div>';
  }
  function renderProductionQueueRow(row) {
    const showPackingBalance = row.current_station === "Packing";
    const packingBalanceQty = Number(row.packing_balance_box_qty || 0);
    const packingBoxQty = Number(row.box_count || 0);
    return `
    <tr>
      <td class="queue-order-cell">
        <div class="queue-order-shell">
          <div class="queue-order-top">
            <div class="queue-order-line">
              <span class="queue-order-number">${escapeHtml(row.order_number)}</span>
              ${pill(row.status)}
              ${row.partial_pending ? '<span class="pill orange">Partially Pending</span>' : ""}
              ${showPackingBalance ? `<span class="pill gray">Boxes ${escapeHtml(String(packingBoxQty))}</span>` : ""}
              ${showPackingBalance && packingBalanceQty > 0 ? `<span class="pill gray">Bal Boxes ${escapeHtml(String(packingBalanceQty))}</span>` : ""}
            </div>
            <div class="queue-order-meta">Current: ${escapeHtml(row.current_station)} | Next: ${escapeHtml(row.next_station)}${row.partial_pending_source ? ` | From: ${escapeHtml(row.partial_pending_source)}` : ""}</div>
          </div>
        </div>
      </td>
      <td>${escapeHtml(row.dealer_name)}</td>
      <td>${escapeHtml(customerLabel(row.customer_name))}</td>
      <td>${escapeHtml(row.order_type)}</td>
      <td>${escapeHtml(row.main_sub)}</td>
      <td>${escapeHtml(row.previous_station)}</td>
      <td>${escapeHtml(row.current_station)}</td>
      <td>${escapeHtml(row.next_station)}</td>
    </tr>
  `;
  }
  function renderPlannerQueueRow(row, canEdit) {
    ensurePlannerColumnOrder();
    const columnLookup = plannerColumnMap();
    return `
    <tr class="planner-row planner-row-${escapeAttribute(row.planner_stage_key)}" draggable="${canEdit ? "true" : "false"}" data-planner-order-id="${row.order_id}">
      ${state.ui.plannerColumnOrder.map((columnId) => {
      var _a;
      return `<td data-label="${escapeAttribute(((_a = columnLookup[columnId]) == null ? void 0 : _a.label) || columnId)}">${renderPlannerCell(row, columnId, canEdit)}</td>`;
    }).join("")}
    </tr>
  `;
  }
  function renderSharedPlanningTable(key, bodyRef, paginationRef, countRef, localSearch = "") {
    if (!state.app || !bodyRef || !paginationRef || !countRef) {
      return;
    }
    let rows = key === "sharedPlanningProduction" || key === "sharedPlanningDataEntry" ? getSharedPlanningRows() : state.app.planning.rows || [];
    if (localSearch) {
      rows = filterPlanningRowsBySearch(rows, localSearch);
    }
    const totalRows = state.app.planning.rows || [];
    renderPagedTable({
      key,
      rows,
      bodyRef,
      paginationRef,
      emptyColumns: 9,
      emptyMessage: "No shared planner rows available.",
      renderRow: (row) => `
      <tr class="planner-row planner-row-${escapeAttribute(row.planner_stage_key)}" data-shared-order-id="${row.order_id}">
        <td>${escapeHtml(row.order_number)}</td>
        <td>${escapeHtml(row.dealer_name)}</td>
        <td>${escapeHtml(customerLabel(row.customer_name))}</td>
        <td>${escapeHtml(row.planner_stage_label)}</td>
        <td>${escapeHtml(row.sla_date || "-")}</td>
        <td>${escapeHtml(row.urgency || "-")}</td>
        <td>${escapeHtml(row.priority || "-")}</td>
        <td>${escapeHtml(row.visible_stations)}</td>
        <td>${escapeHtml(row.planner_remarks || "-")}${row.partial_pending ? " | Partially Pending" : ""}</td>
      </tr>
    `
    });
    countRef.textContent = key === "sharedPlanningProduction" ? `${rows.length} / ${totalRows.length} Orders` : `${rows.length} Orders`;
  }
  function renderProduction() {
    if (!state.app) {
      return;
    }
    const production = state.app.production;
    state.ui.productionStation = production.selected_station;
    fillSelect(
      refs.productionStationFilter,
      production.available_stations.map((value) => ({ value, label: value })),
      production.selected_station
    );
    refs.productionStationFilter.disabled = state.session.role_name === "Machine User";
    refs.productionSearch.value = state.ui.productionSearch;
    renderProductionActionForm(production.rows);
    renderPackingBoxForm(production.rows);
    renderPagedTable({
      key: "production",
      rows: production.rows,
      bodyRef: refs.productionBody,
      paginationRef: refs.productionPagination,
      emptyColumns: 8,
      emptyMessage: "No orders visible in this station.",
      renderRow: renderProductionQueueRow
    });
    refs.productionCount.textContent = `${production.rows.length} Visible`;
    renderSharedPlanningTable("sharedPlanningProduction", refs.sharedPlanningProductionBody, refs.sharedPlanningProductionPagination, refs.sharedPlanningProductionCount);
  }
  function productionOrderPickerLabel(row) {
    return String(row.order_number || "").trim();
  }
  function findProductionRowByPickerValue(rows, value) {
    const text = String(value || "").trim();
    if (!text) {
      return null;
    }
    const exact = (rows || []).find((row) => {
      const orderNumber = String(row.order_number || "").trim();
      return orderNumber.toLowerCase() === text.toLowerCase();
    });
    if (exact) {
      return exact;
    }
    const prefixMatches = (rows || []).filter((row) => {
      const orderNumber = String(row.order_number || "").trim().toLowerCase();
      return orderNumber.startsWith(text.toLowerCase());
    });
    return prefixMatches.length === 1 ? prefixMatches[0] : null;
  }
  function productionOrderHelperText(row) {
    if (!row) {
      return "Type or pick an order number.";
    }
    const parts = [
      row.partial_pending ? "Partial Pending" : "",
      row.dealer_name || "",
      row.customer_name ? `(${row.customer_name})` : "",
      row.order_type || ""
    ].filter(Boolean);
    return parts.join(" | ");
  }
  function renderProductionActionForm(rows) {
    if (!refs.productionActionForm || !refs.productionActionOrder || !refs.productionActionOrderOptions) {
      return;
    }
    const actionRows = (rows || []).filter((row) => row.actions_allowed);
    if (!actionRows.length) {
      refs.productionActionForm.classList.add("hidden");
      syncProductionQrState(null, actionRows);
      return;
    }
    refs.productionActionForm.classList.remove("hidden");
    fillDatalist(refs.productionActionOrderOptions, actionRows.map(productionOrderPickerLabel));
    if (!refs.productionActionOrder.value.trim()) {
      refs.productionActionOrder.value = productionOrderPickerLabel(actionRows[0]);
    }
    const selected = findProductionRowByPickerValue(actionRows, refs.productionActionOrder.value);
    if (refs.productionActionOrderHelper) {
      refs.productionActionOrderHelper.textContent = productionOrderHelperText(selected || actionRows[0]);
    }
    const placeholderRow = selected || actionRows[0];
    refs.productionActionRemarks.placeholder = placeholderRow.current_station === "Packing" ? "Packing: enter box qty" : "Enter remarks";
    syncProductionQrState(placeholderRow, actionRows);
  }
  function renderPackingBoxForm(rows) {
    var _a, _b;
    if (!refs.packingBoxForm || !refs.packingBoxOrder || !refs.packingBoxQtyForm || !refs.packingBoxFormEmpty) {
      return;
    }
    const isPackingSession = ((_a = state.session) == null ? void 0 : _a.role_name) === "Machine User" && ((_b = state.session) == null ? void 0 : _b.station_name) === "Packing";
    const packingRows = (rows || []).filter((row) => row.current_station === "Packing");
    if (!isPackingSession) {
      refs.packingBoxForm.classList.add("hidden");
      refs.packingBoxFormEmpty.classList.add("hidden");
      return;
    }
    if (!packingRows.length) {
      refs.packingBoxForm.classList.add("hidden");
      refs.packingBoxFormEmpty.classList.remove("hidden");
      return;
    }
    refs.packingBoxForm.classList.remove("hidden");
    refs.packingBoxFormEmpty.classList.add("hidden");
    fillDatalist(refs.packingBoxOrderOptions, packingRows.map(productionOrderPickerLabel));
    if (!refs.packingBoxOrder.value.trim()) {
      refs.packingBoxOrder.value = productionOrderPickerLabel(packingRows[0]);
    }
    const selected = findProductionRowByPickerValue(packingRows, refs.packingBoxOrder.value) || packingRows[0];
    refs.packingBoxQtyForm.value = String(selected.box_count || 0);
  }
  async function onPackingBoxFormSave(event) {
    var _a, _b;
    event.preventDefault();
    const selected = findProductionRowByPickerValue(((_b = (_a = state.app) == null ? void 0 : _a.production) == null ? void 0 : _b.rows) || [], refs.packingBoxOrder.value);
    if (!selected) {
      showMessage("Select valid packing order.", "error");
      return;
    }
    try {
      await apiPost("/api/packing/boxes-set", {
        order_id: Number(selected.order_id),
        station_name: "Packing",
        box_qty: refs.packingBoxQtyForm.value || "0"
      });
      await loadAppState(false);
      showMessage("Packing box qty saved.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  function isQrStationAllowed(stationName) {
    return QR_ALLOWED_STATIONS.has(String(stationName || "").trim());
  }
  function syncProductionQrState(selectedRow, rows) {
    var _a;
    if (!refs.productionQrLaunch || !refs.productionQrNote) {
      return;
    }
    const stationName = (selectedRow == null ? void 0 : selectedRow.current_station) || state.ui.productionStation || ((_a = state.session) == null ? void 0 : _a.station_name) || "";
    const canShow = QR_LOCAL_ENABLED && isQrStationAllowed(stationName) && (rows || []).length > 0;
    refs.productionQrLaunch.classList.toggle("hidden", !canShow);
    refs.productionQrNote.classList.toggle("hidden", !canShow);
    if (!canShow) {
      return;
    }
    refs.productionQrNote.textContent = `Local QR mode on for ${stationName}. Open URL with ?qr=1 only.`;
  }
  function extractQrOrderValue(rawText) {
    const text = String(rawText || "").trim();
    if (!text) {
      return "";
    }
    try {
      const parsed = JSON.parse(text);
      return String(parsed.order_number || parsed.orderNo || parsed.order || parsed.code || "").trim() || text;
    } catch (e) {
    }
    try {
      const url = new URL(text);
      return String(
        url.searchParams.get("order_number") || url.searchParams.get("order") || url.searchParams.get("code") || ""
      ).trim() || text;
    } catch (e) {
    }
    return text.replace(/^ORDER[:=\s-]*/i, "").trim();
  }
  function findProductionRowFromQr(rawText) {
    var _a, _b;
    const orderValue = extractQrOrderValue(rawText);
    if (!orderValue) {
      return null;
    }
    const rows = (((_b = (_a = state.app) == null ? void 0 : _a.production) == null ? void 0 : _b.rows) || []).filter((row) => row.actions_allowed && isQrStationAllowed(row.current_station));
    const exact = rows.find((row) => String(row.order_number || "").trim().toLowerCase() === orderValue.toLowerCase());
    if (exact) {
      return exact;
    }
    return rows.find((row) => productionOrderPickerLabel(row).toLowerCase().includes(orderValue.toLowerCase()));
  }
  function applyQrSelection(rawText) {
    var _a, _b;
    const match = findProductionRowFromQr(rawText);
    if (!match) {
      if (refs.qrResultHelper) {
        refs.qrResultHelper.textContent = "QR text found, but no visible machine order matched.";
      }
      showMessage("QR did not match visible machine order.", "error");
      return;
    }
    refs.productionActionOrder.value = productionOrderPickerLabel(match);
    renderProductionActionForm(((_b = (_a = state.app) == null ? void 0 : _a.production) == null ? void 0 : _b.rows) || []);
    if (refs.qrManualInput) {
      refs.qrManualInput.value = extractQrOrderValue(rawText);
    }
    if (refs.qrResultHelper) {
      refs.qrResultHelper.textContent = `Matched ${match.order_number} at ${match.current_station}.`;
    }
    closeQrModal();
    showMessage(`QR matched ${match.order_number}.`, "success");
  }
  function ensureQrScript() {
    if (window.Html5Qrcode) {
      return Promise.resolve();
    }
    if (qrScriptLoadingPromise) {
      return qrScriptLoadingPromise;
    }
    qrScriptLoadingPromise = new Promise((resolve, reject) => {
      const script = document.createElement("script");
      script.src = "https://unpkg.com/html5-qrcode@2.3.8/html5-qrcode.min.js";
      script.onload = resolve;
      script.onerror = () => reject(new Error("QR scanner library did not load."));
      document.head.appendChild(script);
    });
    return qrScriptLoadingPromise;
  }
  async function startQrScanner() {
    if (!refs.qrResultHelper || !refs.qrModalCopy) {
      return;
    }
    refs.qrResultHelper.textContent = "";
    refs.qrModalCopy.textContent = "Point phone camera at order QR. Plain order number works best.";
    await ensureQrScript();
    if (!window.Html5Qrcode) {
      throw new Error("QR scanner library not available.");
    }
    if (qrScannerInstance) {
      try {
        await qrScannerInstance.stop();
      } catch (e) {
      }
      try {
        await qrScannerInstance.clear();
      } catch (e) {
      }
    }
    qrScannerInstance = new window.Html5Qrcode("qr-reader");
    await qrScannerInstance.start(
      { facingMode: "environment" },
      { fps: 10, qrbox: { width: 220, height: 220 } },
      (decodedText) => {
        applyQrSelection(decodedText);
      },
      () => {
      }
    );
  }
  async function restartQrScanner() {
    try {
      await startQrScanner();
      if (refs.qrResultHelper) {
        refs.qrResultHelper.textContent = "Camera ready.";
      }
    } catch (error) {
      if (refs.qrResultHelper) {
        refs.qrResultHelper.textContent = error.message;
      }
    }
  }
  async function openQrModal() {
    var _a, _b, _c, _d, _e;
    if (!QR_LOCAL_ENABLED) {
      showMessage("QR mode hidden. Open local URL with ?qr=1.", "error");
      return;
    }
    const selected = findProductionRowByPickerValue(((_b = (_a = state.app) == null ? void 0 : _a.production) == null ? void 0 : _b.rows) || [], refs.productionActionOrder.value) || (((_d = (_c = state.app) == null ? void 0 : _c.production) == null ? void 0 : _d.rows) || []).find((row) => row.actions_allowed);
    if (!selected || !isQrStationAllowed(selected.current_station)) {
      showMessage("QR allowed only for Hot Press, Cutting, Edgebanding, Drilling, Packing.", "error");
      return;
    }
    state.ui.qrModalOpen = true;
    (_e = refs.qrModal) == null ? void 0 : _e.classList.remove("hidden");
    if (refs.qrManualInput) {
      refs.qrManualInput.value = "";
    }
    if (refs.qrModalCopy) {
      refs.qrModalCopy.textContent = `Scan QR for ${selected.current_station}. Plain order number QR works best.`;
    }
    await restartQrScanner();
  }
  async function stopQrScanner() {
    if (!qrScannerInstance) {
      return;
    }
    try {
      await qrScannerInstance.stop();
    } catch (e) {
    }
    try {
      await qrScannerInstance.clear();
    } catch (e) {
    }
    qrScannerInstance = null;
  }
  function closeQrModal() {
    var _a;
    state.ui.qrModalOpen = false;
    (_a = refs.qrModal) == null ? void 0 : _a.classList.add("hidden");
    stopQrScanner();
  }
  function applyManualQrValue() {
    var _a;
    const rawText = ((_a = refs.qrManualInput) == null ? void 0 : _a.value) || "";
    if (!rawText.trim()) {
      if (refs.qrResultHelper) {
        refs.qrResultHelper.textContent = "Enter QR text first.";
      }
      return;
    }
    applyQrSelection(rawText);
  }
  async function onProductionActionFormClick(event) {
    var _a, _b;
    const button = event.target.closest("[data-production-form-action]");
    if (!button) {
      return;
    }
    const selected = findProductionRowByPickerValue(((_b = (_a = state.app) == null ? void 0 : _a.production) == null ? void 0 : _b.rows) || [], refs.productionActionOrder.value);
    if (!selected) {
      showMessage("Select valid order.", "error");
      return;
    }
    const fieldValue = refs.productionActionRemarks.value.trim();
    const isPacking = selected.current_station === "Packing";
    const maybeBoxQty = isPacking ? Number(fieldValue || "0") : 0;
    try {
      if (isPacking && fieldValue && !Number.isFinite(maybeBoxQty)) {
        showMessage("Enter valid box qty for Packing.", "error");
        return;
      }
      if (isPacking && fieldValue && maybeBoxQty < 0) {
        showMessage("Box qty must be 0 or more.", "error");
        return;
      }
      if (isPacking && fieldValue) {
        await apiPost("/api/packing/boxes-set", {
          order_id: Number(selected.order_id),
          station_name: "Packing",
          box_qty: String(maybeBoxQty)
        });
      }
      await apiPost("/api/production/action", {
        order_id: Number(selected.order_id),
        station_name: selected.current_station,
        action_code: button.dataset.productionFormAction,
        remarks: isPacking ? "" : fieldValue,
        balance_box_qty: selected.current_station === "Packing" ? selected.packing_balance_box_qty || 0 : ""
      });
      refs.productionActionRemarks.value = "";
      await loadAppState(false);
      showMessage(`Production updated for ${selected.current_station}.`, "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  function renderDispatchQueueRow(row) {
    const expanded = state.ui.dispatchExpandedOrderId === row.order_id;
    const dispatchBalanceQty = Number(row.dispatch_balance_box_qty || 0);
    const boxButtons = (row.boxes || []).map((box) => {
      const cls = {
        NONE: "dispatch-box-none",
        LOADED: "dispatch-box-loaded",
        REMOVED: "dispatch-box-removed",
        DOUBT: "dispatch-box-doubt"
      }[box.state] || "dispatch-box-none";
      return `<button class="dispatch-box-button ${cls}" type="button" data-dispatch-box="${row.order_id}" data-box-no="${box.box_no}" title="Box ${box.box_no}">${box.box_no}</button>`;
    }).join("");
    return `
    <tr>
      <td class="queue-order-cell">
        <div class="queue-order-shell">
          <div class="queue-order-top">
            <div class="queue-order-line">
              <button class="queue-order-link" type="button" data-dispatch-expand="${row.order_id}">${escapeHtml(row.order_number)}</button>
              ${pill(row.dispatch_status)}
              ${dispatchBalanceQty > 0 ? `<span class="pill gray">Bal Boxes ${escapeHtml(String(dispatchBalanceQty))}</span>` : ""}
            </div>
            <div class="queue-order-meta">Packing Ready: ${escapeHtml(row.packing_ready_date)}</div>
          </div>
          <label class="queue-inline-field">
            <span>Vehicle</span>
            <input type="text" id="dispatch-vehicle-${row.order_id}" value="${escapeAttribute(row.vehicle_details || "")}" placeholder="Vehicle / transport details">
          </label>
          <label class="queue-inline-field">
            <span>Remarks</span>
            <input type="text" id="dispatch-remark-${row.order_id}" value="${escapeAttribute(row.remarks || "")}" placeholder="Remarks">
          </label>
          <div class="action-inline">
            <button class="status-button primary" type="button" data-dispatch-action="PENDING_DISPATCH" data-order-id="${row.order_id}">Pending</button>
            <button class="status-button warn" type="button" data-dispatch-action="PARTIALLY_DISPATCHED" data-order-id="${row.order_id}">Partial</button>
            <button class="status-button danger" type="button" data-dispatch-action="HOLD" data-order-id="${row.order_id}">Hold</button>
            <button class="status-button success" type="button" data-dispatch-action="DISPATCHED" data-order-id="${row.order_id}">Dispatched</button>
          </div>
          ${expanded ? `
            <div class="dispatch-box-panel">
              <label class="queue-inline-field">
                <span>Balance Box Qty</span>
                <input type="number" min="0" step="1" id="dispatch-balance-${row.order_id}" value="${escapeAttribute(String(dispatchBalanceQty || 0))}" placeholder="0">
              </label>
              <div class="action-inline">
                <button class="micro-button" type="button" data-dispatch-balance-save="${row.order_id}">Save Balance</button>
              </div>
              <div class="dispatch-box-head">
                <strong>Packets: ${row.box_count || 0}</strong>
                <button class="micro-button" type="button" data-dispatch-box-add="${row.order_id}">Add Box</button>
              </div>
              <div class="dispatch-box-grid">
                ${boxButtons || '<span class="dispatch-box-empty">No boxes yet.</span>'}
              </div>
            </div>
          ` : ""}
        </div>
      </td>
      <td>${escapeHtml(row.dealer_name)}</td>
      <td>${escapeHtml(customerLabel(row.customer_name))}</td>
      <td>${escapeHtml(row.packing_ready_date)}</td>
    </tr>
  `;
  }
  function renderDispatch() {
    var _a, _b;
    if (!state.app) {
      return;
    }
    const dispatchSearch = (((_a = refs.dispatchSearch) == null ? void 0 : _a.value) || "").trim().toLowerCase();
    const rows = dispatchSearch ? (state.app.dispatch.rows || []).filter((row) => matchesSearch([
      row.order_number,
      row.dealer_name,
      row.customer_name,
      row.packing_ready_date,
      row.dispatch_status
    ], dispatchSearch)) : state.app.dispatch.rows;
    renderPagedTable({
      key: "dispatch",
      rows,
      bodyRef: refs.dispatchBody,
      paginationRef: refs.dispatchPagination,
      emptyColumns: 4,
      emptyMessage: "No orders waiting for dispatch.",
      renderRow: renderDispatchQueueRow
    });
    refs.dispatchCount.textContent = `${rows.length} Visible`;
    renderSharedPlanningTable("sharedPlanningDispatch", refs.sharedPlanningDispatchBody, refs.sharedPlanningDispatchPagination, refs.sharedPlanningDispatchCount, ((_b = refs.sharedPlanningDispatchSearch) == null ? void 0 : _b.value) || "");
  }
  function renderReports() {
    var _a, _b, _c;
    if (!state.app) {
      return;
    }
    const reports = state.app.reports;
    const dealerDashboardSearch = (((_a = refs.dealerDashboardSearch) == null ? void 0 : _a.value) || "").trim().toLowerCase();
    const marketingDashboardSearch = (((_b = refs.marketingDashboardSearch) == null ? void 0 : _b.value) || "").trim().toLowerCase();
    const auditSearch = (((_c = refs.auditSearch) == null ? void 0 : _c.value) || "").trim().toLowerCase();
    refs.reportSearch.value = state.ui.reportSearch;
    refs.reportDateFrom.value = state.ui.reportDateFrom;
    refs.reportDateTo.value = state.ui.reportDateTo;
    fillSelect(refs.reportDealerFilter, reports.dealer_filters.map((value) => ({ value, label: value === "all" ? "All" : value })), state.ui.reportDealer);
    fillSelect(refs.reportOrderTypeFilter, reports.order_type_filters.map((value) => ({ value, label: value === "all" ? "All" : value })), state.ui.reportOrderType);
    fillSelect(refs.reportStationFilter, reports.station_filters.map((value) => ({ value, label: value === "all" ? "All" : value })), state.ui.reportStation);
    refs.reportStatusFilter.value = state.ui.reportStatus;
    refs.reportSort.value = state.ui.reportSort;
    renderWeeklySummary(reports.weekly_summary);
    renderPagedTable({
      key: "dealerDashboard",
      rows: dealerDashboardSearch ? (reports.dealer_dashboard_rows || []).filter((row) => matchesSearch([
        row.dealer_code,
        row.dealer_name,
        row.customer_type,
        row.marketing_owner,
        row.active_orders,
        row.in_production,
        row.dispatch_ready,
        row.last_updated
      ], dealerDashboardSearch)) : reports.dealer_dashboard_rows || [],
      bodyRef: refs.dealerDashboardBody,
      paginationRef: refs.dealerDashboardPagination,
      emptyColumns: 8,
      emptyMessage: "No dealer dashboard rows available.",
      renderRow: (row) => `
      <tr>
        <td>${escapeHtml(row.dealer_code || "-")}</td>
        <td>${escapeHtml(row.dealer_name || "-")}</td>
        <td>${escapeHtml(row.customer_type || "-")}</td>
        <td>${escapeHtml(row.marketing_owner || "-")}</td>
        <td>${escapeHtml(String(row.active_orders || 0))}</td>
        <td>${escapeHtml(String(row.in_production || 0))}</td>
        <td>${escapeHtml(String(row.dispatch_ready || 0))}</td>
        <td>${escapeHtml(row.last_updated || "-")}</td>
      </tr>
    `
    });
    refs.dealerDashboardCount.textContent = `${dealerDashboardSearch ? (reports.dealer_dashboard_rows || []).filter((row) => matchesSearch([
      row.dealer_code,
      row.dealer_name,
      row.customer_type,
      row.marketing_owner,
      row.active_orders,
      row.in_production,
      row.dispatch_ready,
      row.last_updated
    ], dealerDashboardSearch)).length : (reports.dealer_dashboard_rows || []).length} Dealers`;
    renderPagedTable({
      key: "marketingDashboard",
      rows: marketingDashboardSearch ? (reports.marketing_dashboard_rows || []).filter((row) => matchesSearch([
        row.marketing_owner,
        row.dealer_count,
        row.active_orders,
        row.high_priority,
        row.dispatch_ready,
        row.dispatched_today,
        row.last_updated
      ], marketingDashboardSearch)) : reports.marketing_dashboard_rows || [],
      bodyRef: refs.marketingDashboardBody,
      paginationRef: refs.marketingDashboardPagination,
      emptyColumns: 7,
      emptyMessage: "No marketing dashboard rows available.",
      renderRow: (row) => `
      <tr>
        <td>${escapeHtml(row.marketing_owner || "-")}</td>
        <td>${escapeHtml(String(row.dealer_count || 0))}</td>
        <td>${escapeHtml(String(row.active_orders || 0))}</td>
        <td>${escapeHtml(String(row.high_priority || 0))}</td>
        <td>${escapeHtml(String(row.dispatch_ready || 0))}</td>
        <td>${escapeHtml(String(row.dispatched_today || 0))}</td>
        <td>${escapeHtml(row.last_updated || "-")}</td>
      </tr>
    `
    });
    refs.marketingDashboardCount.textContent = `${marketingDashboardSearch ? (reports.marketing_dashboard_rows || []).filter((row) => matchesSearch([
      row.marketing_owner,
      row.dealer_count,
      row.active_orders,
      row.high_priority,
      row.dispatch_ready,
      row.dispatched_today,
      row.last_updated
    ], marketingDashboardSearch)).length : (reports.marketing_dashboard_rows || []).length} Owners`;
    renderPagedTable({
      key: "reports",
      rows: reports.rows,
      bodyRef: refs.reportsBody,
      paginationRef: refs.reportsPagination,
      emptyColumns: 9,
      emptyMessage: "No report rows match the filter.",
      renderRow: (row) => `
        <tr>
          <td>${escapeHtml(row.order_number)}</td>
          <td>${escapeHtml(row.dealer_name)}</td>
          <td>${escapeHtml(customerLabel(row.customer_name))}</td>
          <td>${escapeHtml(row.order_type)}</td>
          <td>${pill(row.workflow_stage)}</td>
          <td>${escapeHtml(row.visible_stations)}</td>
          <td>${escapeHtml(row.last_action)}</td>
          <td>${escapeHtml(row.updated_at)}</td>
          <td><button class="micro-button" type="button" data-lifecycle-order="${row.order_id}">View</button></td>
        </tr>
      `
    });
    renderPagedTable({
      key: "audit",
      rows: auditSearch ? (reports.audit_logs || []).filter((log) => matchesSearch([
        log.created_at,
        log.user_name,
        log.record_key,
        log.module_name,
        log.action_name,
        log.remarks
      ], auditSearch)) : reports.audit_logs,
      bodyRef: refs.auditBody,
      paginationRef: refs.auditPagination,
      emptyColumns: 6,
      emptyMessage: "No audit logs yet.",
      renderRow: (log) => `
        <tr>
          <td>${escapeHtml(log.created_at)}</td>
          <td>${escapeHtml(log.user_name)}</td>
          <td>${escapeHtml(log.record_key)}</td>
          <td>${escapeHtml(log.module_name)}</td>
          <td>${escapeHtml(log.action_name)}</td>
          <td>${escapeHtml(log.remarks || "-")}</td>
        </tr>
      `
    });
    refs.reportCount.textContent = `${reports.rows.length} Rows`;
    refs.auditCount.textContent = `${auditSearch ? (reports.audit_logs || []).filter((log) => matchesSearch([
      log.created_at,
      log.user_name,
      log.record_key,
      log.module_name,
      log.action_name,
      log.remarks
    ], auditSearch)).length : reports.audit_logs.length} Logs`;
    renderLifecycleBlock(refs.lifecycleTitle, refs.lifecycleDetail);
  }
  function renderWeeklySummary(summary) {
    var _a, _b, _c;
    const weeklyDailySearch = (((_a = refs.weeklyDailySearch) == null ? void 0 : _a.value) || "").trim().toLowerCase();
    const weeklyModuleSearch = (((_b = refs.weeklyModuleSearch) == null ? void 0 : _b.value) || "").trim().toLowerCase();
    const weeklyRecentSearch = (((_c = refs.weeklyRecentSearch) == null ? void 0 : _c.value) || "").trim().toLowerCase();
    if (!summary) {
      refs.weeklyRangeLabel.textContent = "Last 7 Days";
      refs.weeklyMetrics.innerHTML = "";
      refs.weeklyDailyBody.innerHTML = emptyRow(6, "No weekly summary available.");
      refs.weeklyModuleBody.innerHTML = emptyRow(4, "No module activity available.");
      refs.weeklyRecentBody.innerHTML = emptyRow(6, "No weekly activity available.");
      refs.weeklyModuleCount.textContent = "0 Modules";
      refs.weeklyRecentCount.textContent = "0 Logs";
      return;
    }
    refs.weeklyRangeLabel.textContent = summary.range_label || "Last 7 Days";
    refs.weeklyMetrics.innerHTML = [
      metricCard("Orders Updated", summary.orders_updated, "Orders touched in the last 7 days."),
      metricCard("Activity Logs", summary.activity_logs, "All tracked actions in the weekly window."),
      metricCard("Quotations", summary.quotations, "New quotation entries created."),
      metricCard("Confirmations", summary.confirmations, "Orders moved into confirmed stage."),
      metricCard("Optimisation", summary.optimisations, "Optimisation saves completed."),
      metricCard("Procurement", summary.procurement, "Procurement updates recorded."),
      metricCard("Production", summary.production, "Production actions captured."),
      metricCard("Dispatch", summary.dispatch, "Dispatch actions captured.")
    ].join("");
    const dailyRows = weeklyDailySearch ? (summary.daily_rows || []).filter((row) => matchesSearch([
      row.date_label,
      row.actions_total,
      row.orders_updated,
      row.quotations,
      row.production,
      row.dispatch
    ], weeklyDailySearch)) : summary.daily_rows || [];
    refs.weeklyDailyBody.innerHTML = dailyRows.length ? dailyRows.map((row) => `
        <tr>
          <td>${escapeHtml(row.date_label)}</td>
          <td>${escapeHtml(row.actions_total)}</td>
          <td>${escapeHtml(row.orders_updated)}</td>
          <td>${escapeHtml(row.quotations)}</td>
          <td>${escapeHtml(row.production)}</td>
          <td>${escapeHtml(row.dispatch)}</td>
        </tr>
      `).join("") : emptyRow(6, "No daily activity in the last 7 days.");
    const moduleRows = weeklyModuleSearch ? (summary.module_rows || []).filter((row) => matchesSearch([
      row.module_name,
      row.action_count,
      row.last_activity,
      row.last_user
    ], weeklyModuleSearch)) : summary.module_rows || [];
    refs.weeklyModuleBody.innerHTML = moduleRows.length ? moduleRows.map((row) => `
        <tr>
          <td>${escapeHtml(row.module_name)}</td>
          <td>${escapeHtml(row.action_count)}</td>
          <td>${escapeHtml(row.last_activity)}</td>
          <td>${escapeHtml(row.last_user)}</td>
        </tr>
      `).join("") : emptyRow(4, "No module activity in the last 7 days.");
    const recentRows = weeklyRecentSearch ? (summary.recent_rows || []).filter((row) => matchesSearch([
      row.created_at,
      row.module_name,
      row.record_key,
      row.user_name,
      row.action_name,
      row.remarks
    ], weeklyRecentSearch)) : summary.recent_rows || [];
    refs.weeklyRecentBody.innerHTML = recentRows.length ? recentRows.map((row) => `
        <tr>
          <td>${escapeHtml(row.created_at)}</td>
          <td>${escapeHtml(row.module_name)}</td>
          <td>${escapeHtml(row.record_key)}</td>
          <td>${escapeHtml(row.user_name)}</td>
          <td>${escapeHtml(row.action_name)}</td>
          <td>${escapeHtml(row.remarks || "-")}</td>
        </tr>
      `).join("") : emptyRow(6, "No recent weekly activity.");
    refs.weeklyModuleCount.textContent = `${moduleRows.length} Modules`;
    refs.weeklyRecentCount.textContent = `${recentRows.length} Logs`;
  }
  function renderHistory() {
    var _a;
    if (!state.app) {
      return;
    }
    const history = state.app.history;
    const historySearch = (((_a = refs.historySearch) == null ? void 0 : _a.value) || "").trim().toLowerCase();
    renderPagedTable({
      key: "history",
      rows: historySearch ? (history.rows || []).filter((row) => matchesSearch([
        row.acted_at,
        row.order_number,
        row.acted_by,
        row.station_name,
        row.action,
        row.remarks
      ], historySearch)) : history.rows,
      bodyRef: refs.historyBody,
      paginationRef: refs.historyPagination,
      emptyColumns: 7,
      emptyMessage: "No history rows available.",
      renderRow: (row) => `
        <tr>
          <td>${escapeHtml(row.acted_at)}</td>
          <td>${escapeHtml(row.order_number)}</td>
          <td>${escapeHtml(row.acted_by)}</td>
          <td>${escapeHtml(row.station_name)}</td>
          <td>${pill(row.action)}</td>
          <td>${escapeHtml(row.remarks || "-")}</td>
          <td><button class="micro-button" type="button" data-history-order="${row.order_id}">View</button></td>
        </tr>
      `
    });
    refs.historyCount.textContent = `${historySearch ? (history.rows || []).filter((row) => matchesSearch([
      row.acted_at,
      row.order_number,
      row.acted_by,
      row.station_name,
      row.action,
      row.remarks
    ], historySearch)).length : history.rows.length} Rows`;
    renderLifecycleBlock(refs.historyLifecycleTitle, refs.historyLifecycleDetail);
  }
  function renderEmailLog() {
    var _a, _b, _c;
    if (!state.app || !refs.emailLogBody) {
      return;
    }
    const emailLogSearch = (((_a = refs.emailLogSearch) == null ? void 0 : _a.value) || "").trim().toLowerCase();
    const rows = emailLogSearch ? (((_b = state.app.email_log) == null ? void 0 : _b.rows) || []).filter((row) => matchesSearch([
      row.sent_at,
      row.report_kind,
      row.subject_line,
      row.recipient_list,
      row.send_status,
      row.error_text
    ], emailLogSearch)) : ((_c = state.app.email_log) == null ? void 0 : _c.rows) || [];
    renderPagedTable({
      key: "emailLog",
      rows,
      bodyRef: refs.emailLogBody,
      paginationRef: refs.emailLogPagination,
      emptyColumns: 6,
      emptyMessage: "No email logs yet.",
      renderRow: (row) => `
        <tr>
          <td>${escapeHtml(row.sent_at || "-")}</td>
          <td>${escapeHtml(row.report_kind || "-")}</td>
          <td>${escapeHtml(row.subject_line || "-")}</td>
          <td>${escapeHtml(row.recipient_list || "-")}</td>
          <td>${pill(row.send_status || "-")}</td>
          <td>${escapeHtml(row.error_text || "-")}</td>
        </tr>
      `
    });
    refs.emailLogCount.textContent = `${rows.length} Mails`;
  }
  async function onSendHourlyProductionMail() {
    try {
      refs.sendHourlyProductionMail.disabled = true;
      const result = await apiPost("/api/mail/send-hourly-production", {});
      await loadAppState(false);
      showMessage(result.message || "Hourly production mail sent.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    } finally {
      refs.sendHourlyProductionMail.disabled = false;
    }
  }
  function renderLifecycleBlock(titleRef, detailRef) {
    const lifecycle = state.app.lifecycle;
    titleRef.textContent = lifecycle.title || "No Order Selected";
    if (!lifecycle.summary) {
      detailRef.innerHTML = detailPlaceholder("Choose an order to inspect lifecycle.");
      return;
    }
    detailRef.innerHTML = lifecycleMarkup(lifecycle);
  }
  function lifecycleMarkup(lifecycle) {
    const summary = lifecycle.summary;
    const orderPath = [summary.main_order, summary.sub_order].filter((value) => value && value !== "-").join(" / ");
    return `
    <div class="detail-card">
      <strong>Order Summary</strong>
      <div class="detail-line">${escapeHtml(dealerWithCustomer(summary.dealer_name, summary.customer_name))}</div>
      <div class="detail-line">${escapeHtml(summary.customer_type)} | ${escapeHtml(summary.order_type)}</div>
      <div class="detail-line">${escapeHtml(orderPath || "-")}</div>
      <div class="detail-line">${escapeHtml(summary.workflow_stage)}</div>
      <div class="detail-line">Visible: ${escapeHtml(summary.visible_stations)}</div>
    </div>
    <div class="detail-card">
      <strong>Station Remarks</strong>
      <div class="detail-list">
        ${lifecycle.station_remarks.length ? lifecycle.station_remarks.map((item) => `<div class="detail-line"><strong>${escapeHtml(item.station_name)}</strong> ${escapeHtml(item.remarks || "-")}</div>`).join("") : '<div class="detail-line">No station remarks.</div>'}
      </div>
    </div>
    <div class="detail-card">
      <strong>History</strong>
      <div class="detail-list">
        ${lifecycle.history.length ? lifecycle.history.map((item) => `<div class="detail-line">${escapeHtml(item.acted_at)} | ${escapeHtml(item.acted_by)} | ${escapeHtml(item.station_name)} | ${escapeHtml(item.action)}${item.remarks ? ` | ${escapeHtml(item.remarks)}` : ""}</div>`).join("") : '<div class="detail-line">No movement history.</div>'}
      </div>
    </div>
    <div class="detail-card">
      <strong>Dispatch</strong>
      <div class="detail-line">${escapeHtml(summary.dispatch_status)}</div>
      <div class="detail-line">${escapeHtml(summary.visible_stations)}</div>
    </div>
  `;
  }
  function renderHistoryModal() {
    var _a, _b;
    if (!state.ui.historyModalOpen || !((_b = (_a = state.app) == null ? void 0 : _a.lifecycle) == null ? void 0 : _b.summary)) {
      refs.historyModal.classList.add("hidden");
      return;
    }
    refs.historyModal.classList.remove("hidden");
    refs.historyModalTitle.textContent = state.app.lifecycle.title || "Order History";
    refs.historyModalBody.innerHTML = lifecycleMarkup(state.app.lifecycle);
  }
  function renderMasters() {
    if (!state.app) {
      return;
    }
    renderDropdownMasterList(refs.dealerTypeList, state.app.masters.dealer_types, "DEALER_TYPE");
    renderDropdownMasterList(refs.paymentTermsList, state.app.masters.payment_terms, "PAYMENT_TERMS");
    renderDropdownMasterList(refs.marketingOwnerList, state.app.masters.marketing_owners, "MARKETING_OWNER");
    renderDropdownMasterList(refs.quotationOwnerList, state.app.masters.quotation_owners, "QUOTATION_OWNER");
    renderDropdownMasterList(refs.orderClassList, state.app.masters.order_classes, "ORDER_CLASS");
    refs.customerTypeList.innerHTML = state.app.masters.customer_types.map((item) => `
    <div class="list-row">
      <span class="list-label">${escapeHtml(item.code)}</span>
      <div class="list-actions">
        <button class="micro-button" type="button" data-master-name="customer_types" data-master-id="${item.id}" data-master-direction="up">Up</button>
        <button class="micro-button" type="button" data-master-name="customer_types" data-master-id="${item.id}" data-master-direction="down">Dn</button>
        <button class="micro-button" type="button" data-master-deactivate="customer_types" data-master-id="${item.id}">X</button>
      </div>
    </div>
  `).join("");
    refs.orderTypeList.innerHTML = state.app.masters.order_types.map((item) => `
    <div class="list-row">
      <span class="list-label">${escapeHtml(item.name)}</span>
      <div class="list-actions">
        <button class="micro-button" type="button" data-master-name="order_types" data-master-id="${item.id}" data-master-direction="up">Up</button>
        <button class="micro-button" type="button" data-master-name="order_types" data-master-id="${item.id}" data-master-direction="down">Dn</button>
        <button class="micro-button" type="button" data-master-deactivate="order_types" data-master-id="${item.id}">X</button>
      </div>
    </div>
  `).join("");
    refs.vendorList.innerHTML = state.app.masters.vendors.map((item) => `
    <div class="list-row">
      <span class="list-label">${escapeHtml(item.name)}</span>
      <div class="list-actions">
        <button class="micro-button" type="button" data-master-deactivate="vendors" data-master-id="${item.id}">X</button>
      </div>
    </div>
  `).join("");
    const machineMarkup = state.app.masters.machines.map((item) => `
    <div class="list-row">
      <span class="list-label">${escapeHtml(item.sequence_no || "-")}. ${escapeHtml(item.name)}</span>
      <div class="list-actions">
        <button class="micro-button" type="button" data-machine-edit="true" data-machine-id="${item.id}" data-machine-name="${escapeAttribute(item.name)}">Ed</button>
        <button class="micro-button" type="button" data-master-name="machines" data-master-id="${item.id}" data-master-direction="up">Up</button>
        <button class="micro-button" type="button" data-master-name="machines" data-master-id="${item.id}" data-master-direction="down">Dn</button>
      </div>
    </div>
  `).join("");
    refs.machineSequenceList.innerHTML = machineMarkup;
    if (refs.plannerStationMasterList) {
      refs.plannerStationMasterList.innerHTML = machineMarkup || '<div class="detail-card">No stations added yet.</div>';
    }
  }
  function renderDropdownMasterList(target, items, masterName) {
    if (!target) {
      return;
    }
    target.innerHTML = items.map((item) => `
    <div class="list-row">
      <span class="list-label">${escapeHtml(item.name)}</span>
      <div class="list-actions">
        <button class="micro-button" type="button" data-dropdown-edit="true" data-dropdown-master="${masterName}" data-dropdown-id="${item.id}" data-dropdown-value="${escapeAttribute(item.name)}">Ed</button>
        <button class="micro-button" type="button" data-dropdown-delete="true" data-dropdown-master="${masterName}" data-dropdown-id="${item.id}">X</button>
      </div>
    </div>
  `).join("");
  }
  function renderUsers() {
    if (!state.app) {
      return;
    }
    if (refs.usersSearch) {
      refs.usersSearch.value = state.ui.usersSearch || "";
    }
    const usersSearch = (state.ui.usersSearch || "").trim().toLowerCase();
    const rows = usersSearch ? state.app.users.filter((user) => [
      user.full_name,
      user.login_id,
      user.role_name,
      user.station_name
    ].some((value) => String(value || "").toLowerCase().includes(usersSearch))) : state.app.users;
    renderPagedTable({
      key: "users",
      rows,
      bodyRef: refs.usersBody,
      paginationRef: refs.usersPagination,
      emptyColumns: 6,
      emptyMessage: "No users available.",
      renderRow: (user) => `
        <tr>
          <td>${escapeHtml(user.full_name)}</td>
          <td>${escapeHtml(user.login_id)}</td>
          <td>${escapeHtml(user.role_name)}</td>
          <td>${escapeHtml(user.station_name)}</td>
          <td>${user.is_active ? '<span class="pill green">Active</span>' : '<span class="pill gray">Inactive</span>'}</td>
          <td><button class="micro-button" type="button" data-user-edit="${user.user_id}">Edit</button> <button class="micro-button" type="button" data-user-toggle="${user.user_id}">${user.is_active ? "Off" : "On"}</button></td>
        </tr>
      `
    });
    renderUserStationOptions();
  }
  function resetUserForm() {
    var _a;
    (_a = document.querySelector("#user-form")) == null ? void 0 : _a.reset();
    if (refs.userId) refs.userId.value = "";
    if (refs.userPassword) refs.userPassword.required = true;
    if (refs.userSaveButton) refs.userSaveButton.textContent = "Create User";
    renderUserStationOptions();
  }
  function startUserEdit(userId) {
    if (!state.app) {
      return;
    }
    const user = state.app.users.find((item) => Number(item.user_id) === Number(userId));
    if (!user) {
      showMessage("User not found.", "error");
      return;
    }
    refs.userId.value = String(user.user_id);
    refs.userName.value = user.full_name || "";
    refs.userLogin.value = user.login_id || "";
    refs.userPassword.value = "";
    refs.userPassword.required = false;
    refs.userRole.value = user.role_name || "Data Entry";
    renderUserStationOptions();
    refs.userStation.value = user.station_name === "All Stations" ? "" : user.station_name || "";
    refs.userSaveButton.textContent = "Update User";
    refs.userName.focus();
  }
  function renderUserStationOptions() {
    var _a;
    if (!state.app) {
      return;
    }
    const role = refs.userRole.value;
    let options = [{ value: "", label: "All Stations" }];
    if (role === "Machine User") {
      options = state.app.masters.machines.filter((item) => item.name !== "Dispatch").map((item) => ({ value: item.name, label: item.name }));
    } else if (role === "Dispatch User") {
      options = [{ value: "Dispatch", label: "Dispatch" }];
    }
    fillSelect(refs.userStation, options, ((_a = options[0]) == null ? void 0 : _a.value) || "");
  }
  function renderSettings() {
    if (!state.app) {
      return;
    }
    refs.currentBuildList.innerHTML = [
      "Access-backed web handler with queue and history tables",
      "Production planner queue with SLA, urgency, priority, and reapproval",
      "Role-based session login with simple browser-compatible password handling",
      "Excel-driven user import from the admin screen",
      "Server-side production and dispatch movement logic"
    ].map((item) => `<li>${escapeHtml(item)}</li>`).join("");
    refs.pathBuildList.innerHTML = [
      `Database: ${state.app.settings.database_path}`,
      `User template: ${state.app.settings.user_template_path}`,
      "Runtime: HTML, CSS, JavaScript, Access-backed web app"
    ].map((item) => `<li>${escapeHtml(item)}</li>`).join("");
  }
  function onCollapseToggle(event) {
    const button = event.currentTarget;
    const target = button.dataset.collapseToggle;
    const body = document.querySelector(`[data-collapse-body="${target}"]`);
    const card = button.closest("[data-collapsible-card]");
    if (!body || !card) {
      return;
    }
    const isOpen = !body.classList.contains("hidden");
    body.classList.toggle("hidden", isOpen);
    card.classList.toggle("is-open", !isOpen);
    button.setAttribute("aria-expanded", String(!isOpen));
    const arrow = button.querySelector(".collapse-arrow");
    if (arrow) {
      arrow.textContent = isOpen ? ">" : "v";
    }
  }
  function openRoleHome() {
    if (!state.session) {
      activateSection("data-entry");
      return;
    }
    activateSection(state.session.home_section || "data-entry");
  }
  function activateSection(sectionName) {
    document.querySelectorAll(".nav-link").forEach((button) => {
      button.classList.toggle("active", button.dataset.section === sectionName);
    });
    document.querySelectorAll(".section-panel").forEach((panel) => {
      panel.classList.toggle("active", panel.dataset.panel === sectionName);
    });
    const activeButton = document.querySelector(`.nav-link[data-section="${sectionName}"]`);
    refs.sectionTitle.textContent = activeButton ? activeButton.textContent : "Workspace";
    if (!isDesktopSidebarViewport()) {
      state.ui.sidebarMobileOpen = false;
      applySidebarLayout();
    }
  }
  function isDesktopSidebarViewport() {
    return window.innerWidth > SIDEBAR_BREAKPOINT;
  }
  function syncSidebarViewport() {
    if (isDesktopSidebarViewport()) {
      state.ui.sidebarMobileOpen = false;
    }
    applySidebarLayout();
  }
  function onSidebarToggle() {
    const isOpen = state.ui.sidebarMobileOpen;
    state.ui.sidebarMobileOpen = !isOpen;
    state.ui.sidebarExpanded = false;
    applySidebarLayout();
  }
  function closeSidebar() {
    state.ui.sidebarMobileOpen = false;
    state.ui.sidebarExpanded = false;
    applySidebarLayout();
  }
  function applySidebarLayout() {
    const isOpen = state.ui.sidebarMobileOpen;
    refs.sidebarPanel.classList.toggle("hidden", !isOpen);
    refs.appShell.classList.toggle("sidebar-expanded", isOpen);
    refs.appShell.classList.toggle("sidebar-mobile-open", isOpen);
    refs.sidebarBackdrop.classList.toggle("hidden", !isOpen);
    refs.sidebarDismiss.classList.toggle("hidden", !isOpen);
    refs.sidebarToggle.classList.toggle("is-active", isOpen);
    refs.sidebarToggle.setAttribute("aria-expanded", String(isOpen));
    refs.sidebarToggle.setAttribute("aria-label", isOpen ? "Close menu" : "Open menu");
    refs.sidebarToggle.setAttribute("title", isOpen ? "Close menu" : "Open menu");
    document.body.classList.toggle("sidebar-open", isOpen);
  }
  function syncQuotationDealerFields() {
    if (!state.app) {
      return;
    }
    const dealerName = refs.quotationDealer.value.trim().toLowerCase();
    const dealer = state.app.data_entry.dealers.find((item) => String(item.dealer_name || "").trim().toLowerCase() === dealerName);
    refs.quotationCustomerType.value = dealer ? dealer.customer_type_code || "" : "";
  }
  function syncQuotationOrderClassFields() {
    const isSubOrder = refs.quotationMainOrder.value.trim().toLowerCase() === "sub order";
    refs.quotationSubOrderWrap.classList.toggle("hidden", !isSubOrder);
    refs.quotationSubOrder.required = isSubOrder;
    fillSelect(
      refs.quotationSubOrder,
      buildMainOrderReferenceOptions(),
      refs.quotationSubOrder.value || ""
    );
    refs.quotationSubOrder.disabled = !isSubOrder || refs.quotationSubOrder.options.length <= 1;
    if (!isSubOrder) {
      refs.quotationSubOrder.value = "";
    }
  }
  function buildMainOrderReferenceOptions() {
    var _a, _b;
    const values = ((_b = (_a = state.app) == null ? void 0 : _a.data_entry) == null ? void 0 : _b.main_order_reference_options) || [];
    const options = [{ value: "", label: values.length ? "Select previous main order" : "No previous main orders available" }];
    values.forEach((value) => {
      options.push({ value, label: value });
    });
    return options;
  }
  function refreshDealerCodePreview() {
    refs.dealerCode.value = nextDealerCodePreview(refs.dealerType.value.trim());
  }
  function nextDealerCodePreview(dealerType) {
    const prefix = normalizeDealerPrefix(dealerType);
    if (!prefix || !state.app) {
      return "";
    }
    const matchingDealers = state.app.data_entry.dealers.filter((dealer) => String(dealer.dealer_type || "").trim().toLowerCase() === dealerType.trim().toLowerCase());
    let max = 1e3;
    matchingDealers.forEach((dealer) => {
      const digits = String(dealer.dealer_code || "").match(/(\d+)\s*$/);
      const numeric = digits ? Number(digits[1]) : 0;
      if (numeric > max) {
        max = numeric;
      }
    });
    return `${prefix} ${max + 1}`;
  }
  function normalizeDealerPrefix(value) {
    return String(value || "").toUpperCase().replace(/[^A-Z0-9]+/g, "");
  }
  function dropdownMasterLabel(masterName) {
    const labels = {
      DEALER_TYPE: "Dealer type",
      PAYMENT_TERMS: "Payment term",
      MARKETING_OWNER: "Marketing owner",
      QUOTATION_OWNER: "Quotation owner",
      ORDER_CLASS: "Order class"
    };
    return labels[masterName] || "Dropdown value";
  }
  async function onDealerSave(event) {
    event.preventDefault();
    try {
      await apiPost("/api/dealers", {
        dealer_name: refs.dealerName.value.trim(),
        company_name: refs.dealerCompany.value.trim(),
        dealer_type: refs.dealerType.value.trim(),
        customer_type: refs.dealerCustomerType.value.trim(),
        city: refs.dealerCity.value.trim(),
        pin_code: refs.dealerPinCode.value.trim(),
        gst_number: refs.dealerGst.value.trim(),
        contact_person: refs.dealerContact.value.trim(),
        mobile_number: refs.dealerMobile.value.trim(),
        email: refs.dealerEmail.value.trim(),
        payment_terms: refs.dealerPaymentTerms.value.trim(),
        credit_limit_lakh: refs.dealerCreditLimit.value.trim(),
        marketing_owner: currentMarketingOwnerValue(),
        address: refs.dealerAddress.value.trim()
      });
      event.target.reset();
      refs.dealerCode.value = "";
      syncDealerMarketingOwnerField();
      await loadAppState(false);
      showMessage("Dealer saved.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onDealerImport(event) {
    event.preventDefault();
    if (!refs.dealerImportFile.files.length) {
      showMessage("Choose a dealer Excel file first.", "error");
      return;
    }
    try {
      if (!window.XLSX) {
        throw new Error("Excel import library did not load.");
      }
      const rowsTsv = await buildDealerImportRowsTsv(refs.dealerImportFile.files[0]);
      if (rowsTsv.split("\n").length <= 1) {
        throw new Error("No valid dealer rows found. Required: dealer name, dealer type, customer type, phone/mobile.");
      }
      const result = await apiPost("/api/dealers/import", { rows_tsv: rowsTsv });
      event.target.reset();
      await loadAppState(false);
      showMessage(`${result.imported} dealers imported.`, "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onDealerRegisterClick(event) {
    const editButton = event.target.closest("[data-dealer-edit]");
    const deleteButton = event.target.closest("[data-dealer-delete]");
    if (!editButton && !deleteButton) return;
    const dealerId = Number((editButton || deleteButton).dataset.dealerEdit || (editButton || deleteButton).dataset.dealerDelete);
    const dealer = state.app.data_entry.dealers.find((item) => Number(item.dealer_id) === dealerId);
    if (!dealer) return;
    try {
      if (deleteButton) {
        if (!confirm(`Delete dealer ${dealer.dealer_name}?`)) return;
        await apiPost("/api/dealers/delete", { dealer_id: dealerId });
        await loadAppState(false);
        showMessage("Dealer deleted.", "success");
        return;
      }
      const dealerName = prompt("Dealer name", dealer.dealer_name || "");
      if (dealerName === null) return;
      const phone = prompt("Phone", dealer.mobile_number || "");
      if (phone === null) return;
      const city = prompt("City", dealer.city || "");
      if (city === null) return;
      await apiPost("/api/dealers/update", {
        dealer_id: dealerId,
        dealer_name: dealerName,
        mobile_number: phone,
        city
      });
      await loadAppState(false);
      showMessage("Dealer updated.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  function downloadDealerTemplate() {
    var _a, _b, _c, _d, _e, _f, _g, _h, _i;
    if (!window.XLSX) {
      showMessage("Excel template library did not load.", "error");
      return;
    }
    const sampleMarketingOwner = isMarketingUserSession() ? ((_a = state.session) == null ? void 0 : _a.full_name) || "Marketing User" : "Sanya Roy";
    const dealerTypes = ((_c = (_b = state.app) == null ? void 0 : _b.masters) == null ? void 0 : _c.dealer_types) || [];
    const customerTypes = ((_e = (_d = state.app) == null ? void 0 : _d.masters) == null ? void 0 : _e.customer_types) || [];
    const paymentTerms = ((_g = (_f = state.app) == null ? void 0 : _f.masters) == null ? void 0 : _g.payment_terms) || [];
    const marketingOwners = ((_i = (_h = state.app) == null ? void 0 : _h.masters) == null ? void 0 : _i.marketing_owners) || [];
    const rows = [
      ["dealer_code", "dealer_name", "company_name", "dealer_type", "customer_type", "city", "pin_code", "gst_number", "contact_person", "phone", "email", "payment_terms", "credit_limit_lakh", "marketing_owner", "address"],
      ["", "Sample Dealer", "Sample Company", "M", "EL", "Bengaluru", "560001", "29ABCDE1234F1Z5", "Sample Person", "9876543210", "sample@dealer.com", "30 days", "15", sampleMarketingOwner, "Sample address"]
    ];
    const workbook = XLSX.utils.book_new();
    const dealerSheet = XLSX.utils.aoa_to_sheet(rows);
    colorMandatoryHeaders(dealerSheet, rows[0], ["dealer_name", "dealer_type", "customer_type", "phone"]);
    XLSX.utils.book_append_sheet(workbook, dealerSheet, "Dealers");
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([["dealer_type"], ...dealerTypes.map((item) => [item.name])]), "Dealer Type Dropdown");
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([["customer_type"], ...customerTypes.map((item) => [item.name])]), "Customer Type Dropdown");
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([["payment_terms"], ...paymentTerms.map((item) => [item.name])]), "Payment Terms Dropdown");
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([["marketing_owner"], ...marketingOwners.map((item) => [item.name])]), "Marketing Owner Dropdown");
    XLSX.writeFile(workbook, "dealer-import-template.xlsx");
  }
  async function onQuotationSave(event) {
    event.preventDefault();
    try {
      await apiPost("/api/orders/quotation", {
        dealer_name: refs.quotationDealer.value.trim(),
        customer_name: document.querySelector("#quotation-customer").value.trim(),
        order_type: document.querySelector("#quotation-order-type").value.trim(),
        main_order: refs.quotationMainOrder.value.trim(),
        sub_order: refs.quotationSubOrder.value.trim(),
        order_number: document.querySelector("#quotation-order-number").value.trim(),
        approx_value: document.querySelector("#quotation-approx-value").value.trim(),
        remarks: document.querySelector("#quotation-remarks").value.trim()
      });
      event.target.reset();
      syncQuotationDealerFields();
      syncQuotationOrderClassFields();
      await loadAppState(false);
      showMessage("Quotation created.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onQuotationImport(event) {
    event.preventDefault();
    if (!refs.quotationImportFile.files.length) {
      showMessage("Choose a quotation Excel file first.", "error");
      return;
    }
    try {
      if (!window.XLSX) {
        throw new Error("Excel import library did not load.");
      }
      const rowsTsv = await buildQuotationImportRowsTsv(refs.quotationImportFile.files[0]);
      if (rowsTsv.split("\n").length <= 1) {
        throw new Error("No valid quotation rows found. Required: dealer name, order type, order class, order number.");
      }
      const result = await apiPost("/api/orders/quotation/import", { rows_tsv: rowsTsv });
      event.target.reset();
      await loadAppState(false);
      showMessage(`${result.imported} quotations imported.`, "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  function canDeleteQuotation(order) {
    if (!state.session || !order) return false;
    if (state.session.role_name === "Admin") return true;
    return Number(order.created_by) === Number(state.session.user_id);
  }
  async function onQuotationRegisterClick(event) {
    var _a, _b, _c;
    const accordionButton = event.target.closest("[data-quotation-accordion]");
    if (accordionButton) {
      const orderId2 = Number(accordionButton.dataset.quotationAccordion);
      const panel = refs.quotationRegisterBody.querySelector(`[data-quotation-panel="${orderId2}"]`);
      if (panel) {
        panel.classList.toggle("hidden");
      }
      return;
    }
    const button = event.target.closest("[data-quotation-delete]");
    if (!button) return;
    const orderId = Number(button.dataset.quotationDelete);
    const order = (_c = (_b = (_a = state.app) == null ? void 0 : _a.data_entry) == null ? void 0 : _b.quotations) == null ? void 0 : _c.find((item) => Number(item.order_id) === orderId);
    if (!order) {
      showMessage("Quotation not found.", "error");
      return;
    }
    if (!window.confirm(`Delete quotation ${order.quotation_number} / ${order.order_number}?`)) {
      return;
    }
    try {
      await apiPost("/api/orders/quotation/delete", { order_id: orderId });
      await loadAppState(false);
      showMessage("Quotation deleted.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  function downloadQuotationTemplate() {
    var _a, _b, _c, _d, _e, _f, _g, _h, _i, _j;
    if (!window.XLSX) {
      showMessage("Excel template library did not load.", "error");
      return;
    }
    const dealers = ((_b = (_a = state.app) == null ? void 0 : _a.data_entry) == null ? void 0 : _b.dealers) || [];
    const orderTypes = ((_d = (_c = state.app) == null ? void 0 : _c.masters) == null ? void 0 : _d.order_types) || [];
    const orderClasses = ((_f = (_e = state.app) == null ? void 0 : _e.masters) == null ? void 0 : _f.order_classes) || [];
    const mainOrders = (((_h = (_g = state.app) == null ? void 0 : _g.data_entry) == null ? void 0 : _h.recent_orders) || []).filter((order) => String(order.order_class || order.main_order || "").toLowerCase() === "main order").map((order) => order.order_number).filter(Boolean);
    const headers = ["dealer_name", "customer_name", "order_type", "order_class", "main_order_reference", "order_number", "approx_value", "remarks"];
    const sampleDealer = ((_i = dealers[0]) == null ? void 0 : _i.dealer_name) || "Sample Dealer";
    const sampleOrderType = ((_j = orderTypes[0]) == null ? void 0 : _j.name) || "Laminate";
    const rows = [
      headers,
      [sampleDealer, "Optional Customer", sampleOrderType, "Main Order", "", "ORD-NEW-001", "100000", "Imported quotation"]
    ];
    const workbook = XLSX.utils.book_new();
    const quoteSheet = XLSX.utils.aoa_to_sheet(rows);
    colorMandatoryHeaders(quoteSheet, headers, ["dealer_name", "order_type", "order_class", "order_number"]);
    XLSX.utils.book_append_sheet(workbook, quoteSheet, "Quotations");
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([["dealer_name", "customer_type"], ...dealers.map((dealer) => [dealer.dealer_name, dealer.customer_type_code || ""])]), "Dealer Dropdown");
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([["order_type"], ...orderTypes.map((item) => [item.name])]), "Order Type Dropdown");
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([["order_class"], ...orderClasses.map((item) => [item.name])]), "Order Class Dropdown");
    XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([["main_order_reference"], ...mainOrders.map((item) => [item])]), "Main Order Dropdown");
    XLSX.writeFile(workbook, "quotation-import-template.xlsx");
  }
  async function onConfirmOrder(event) {
    var _a;
    event.preventDefault();
    try {
      await apiPost("/api/orders/confirm", {
        order_number: document.querySelector("#confirm-order-number").value.trim(),
        confirmation_date: ((_a = refs.confirmDateTime) == null ? void 0 : _a.value) || "",
        remarks: document.querySelector("#confirm-remarks").value.trim()
      });
      event.target.reset();
      ensureConfirmDateTimeDefault(true);
      await loadAppState(false);
      showMessage("Order confirmed.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onOptimisationSave(event) {
    var _a, _b, _c, _d;
    event.preventDefault();
    if (!((_c = (_b = (_a = state.app) == null ? void 0 : _a.optimisation) == null ? void 0 : _b.eligible_order_numbers) == null ? void 0 : _c.length)) {
      showMessage("No orders are available for optimisation.", "error");
      return;
    }
    try {
      await apiPost("/api/orders/optimise", {
        order_number: refs.optimisationOrderNumber.value.trim(),
        optimisation_date: ((_d = refs.optimisationDateTime) == null ? void 0 : _d.value) || "",
        number_of_boards: refs.optimisationBoards.value,
        number_of_panels: refs.optimisationPanels.value,
        rm_details: refs.optimisationRmDetails.value.trim()
      });
      event.target.reset();
      ensureOptimisationDateTimeDefault(true);
      ensureOptimisationDefaults(true);
      await loadAppState(false);
      showMessage("Optimisation saved.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onProcurementSave(event) {
    var _a, _b, _c;
    event.preventDefault();
    if (!((_c = (_b = (_a = state.app) == null ? void 0 : _a.procurement) == null ? void 0 : _b.eligible_order_numbers) == null ? void 0 : _c.length)) {
      showMessage("No orders are available for procurement.", "error");
      return;
    }
    try {
      await apiPost("/api/orders/procurement", {
        order_number: refs.procurementOrderNumber.value.trim(),
        po_number: refs.procurementPoNumber.value.trim(),
        po_date: refs.procurementPoDate.value,
        vendor_name: refs.procurementVendor.value.trim(),
        mrn_date: refs.procurementMrnDate.value,
        procurement_status_code: refs.procurementStatus.value,
        item_details: refs.procurementItemDetails.value.trim(),
        remarks: refs.procurementRemarks.value.trim()
      });
      event.target.reset();
      ensureProcurementDateDefaults(true);
      ensureProcurementDefaults(true);
      await loadAppState(false);
      showMessage("Procurement updated.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onCustomerTypeSave(event) {
    event.preventDefault();
    try {
      await apiPost("/api/masters/customer-types", {
        code: document.querySelector("#customer-type-input").value.trim()
      });
      event.target.reset();
      await loadAppState(false);
      showMessage("Customer type added.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onOrderTypeSave(event) {
    event.preventDefault();
    try {
      await apiPost("/api/masters/order-types", {
        name: document.querySelector("#order-type-input").value.trim()
      });
      event.target.reset();
      await loadAppState(false);
      showMessage("Order type added.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onVendorSave(event) {
    event.preventDefault();
    try {
      await apiPost("/api/masters/vendors", {
        name: document.querySelector("#vendor-input").value.trim()
      });
      event.target.reset();
      await loadAppState(false);
      showMessage("Vendor added.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onDropdownMasterSave(event) {
    event.preventDefault();
    const form = event.currentTarget;
    const input = form.querySelector("[data-dropdown-input]");
    const masterName = form.dataset.dropdownMaster;
    if (!input || !masterName) {
      return;
    }
    try {
      await apiPost("/api/masters/dealer-dropdowns", {
        master_name: masterName,
        value: input.value.trim()
      });
      form.reset();
      await loadAppState(false);
      showMessage(`${dropdownMasterLabel(masterName)} added.`, "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onQuickAddClick(event) {
    const button = event.target.closest("[data-quick-add-master]");
    if (!button) {
      return;
    }
    const masterName = button.dataset.quickAddMaster;
    const targetSelector = button.dataset.quickAddTarget;
    const label = dropdownMasterLabel(masterName);
    const nextValue = window.prompt(`Add new ${label.toLowerCase()}`, "");
    if (!nextValue || !nextValue.trim()) {
      return;
    }
    try {
      await apiPost("/api/masters/dealer-dropdowns", {
        master_name: masterName,
        value: nextValue.trim()
      });
      await loadAppState(false);
      const target = targetSelector ? document.querySelector(targetSelector) : null;
      if (target) {
        target.value = nextValue.trim();
      }
      showMessage(`${label} added.`, "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onUserSave(event) {
    event.preventDefault();
    const isEdit = Boolean(refs.userId.value);
    try {
      await apiPost("/api/users", {
        user_id: refs.userId.value || "",
        full_name: refs.userName.value.trim(),
        login_id: refs.userLogin.value.trim(),
        role_name: refs.userRole.value,
        assigned_station: refs.userStation.value,
        password: refs.userPassword.value
      });
      resetUserForm();
      await loadAppState(false);
      showMessage(isEdit ? "User updated." : "User created.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onPasswordReset(event) {
    event.preventDefault();
    try {
      await apiPost("/api/users/reset-password", {
        login_id: refs.passwordResetLogin.value.trim(),
        password: refs.passwordResetPassword.value
      });
      event.target.reset();
      await loadAppState(false);
      showMessage("Password reset.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onUserImport(event) {
    event.preventDefault();
    const fileInput = document.querySelector("#user-import-file");
    if (!fileInput.files.length) {
      showMessage("Choose an Excel file first.", "error");
      return;
    }
    try {
      if (!window.XLSX) {
        throw new Error("Excel import library did not load.");
      }
      const rowsTsv = await buildImportRowsTsv(fileInput.files[0]);
      const result = await apiPost("/api/users/import", { rows_tsv: rowsTsv });
      event.target.reset();
      await loadAppState(false);
      showMessage(`${result.imported} users imported.`, "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onPlannerMachineSave(event) {
    event.preventDefault();
    try {
      await apiPost("/api/machines/save", {
        machine_name: refs.plannerMachineName.value.trim()
      });
      event.target.reset();
      await loadAppState(false);
      showMessage("Machine station saved.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onPlannerProfileSave(event) {
    event.preventDefault();
    try {
      await apiPost("/api/sequence-profiles/save", {
        profile_name: refs.plannerProfileName.value.trim(),
        order_type_id: refs.plannerSequenceOrderType.value,
        order_class: refs.plannerSequenceOrderClass.value
      });
      refs.plannerProfileName.value = "";
      await loadAppState(false);
      showMessage("Custom sequence saved.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onPlannerSequenceAddStation(event) {
    event.preventDefault();
    try {
      await apiPost("/api/sequence-profiles/add-station", {
        profile_id: refs.plannerProfileSelect.value,
        station_id: refs.plannerSequenceStation.value
      });
      await loadAppState(false);
      showMessage("Station added to sequence.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  function onPlannerProfileSelectChange() {
    state.ui.selectedSequenceProfileId = refs.plannerProfileSelect.value;
    renderPlanning();
  }
  async function onProductionTableClick(event) {
    var _a, _b;
    const historyButton = event.target.closest("[data-open-history]");
    if (historyButton) {
      await openHistoryModalForOrder(Number(historyButton.dataset.openHistory));
      return;
    }
    const balanceExpandButton = event.target.closest("[data-production-balance-expand]");
    if (balanceExpandButton) {
      const orderId2 = Number(balanceExpandButton.dataset.productionBalanceExpand);
      state.ui.productionBalanceOrderId = state.ui.productionBalanceOrderId === orderId2 ? null : orderId2;
      renderProduction();
      return;
    }
    const balanceSaveButton = event.target.closest("[data-production-balance-save]");
    if (balanceSaveButton) {
      const orderId2 = Number(balanceSaveButton.dataset.productionBalanceSave);
      const stationName2 = balanceSaveButton.dataset.stationName || "Packing";
      const balanceBoxQty2 = ((_a = document.querySelector(`#packing-balance-${orderId2}`)) == null ? void 0 : _a.value) || "0";
      try {
        await apiPost("/api/production/balance-save", {
          order_id: orderId2,
          station_name: stationName2,
          balance_box_qty: balanceBoxQty2
        });
        await loadAppState(false);
        showMessage("Packing balance saved.", "success");
      } catch (error) {
        showMessage(error.message, "error");
      }
      return;
    }
    const button = event.target.closest("[data-order-action]");
    if (!button) {
      return;
    }
    const orderId = button.dataset.orderId;
    const stationName = button.dataset.stationName;
    const remarkRef = document.querySelector(`#remark-${orderId}-${safeId(stationName)}`);
    const remarks = remarkRef ? remarkRef.value.trim() : "";
    const balanceBoxQty = stationName === "Packing" ? ((_b = document.querySelector(`#packing-balance-${orderId}`)) == null ? void 0 : _b.value) || "0" : "";
    try {
      await apiPost("/api/production/action", {
        order_id: Number(orderId),
        station_name: stationName,
        action_code: button.dataset.orderAction,
        remarks,
        balance_box_qty: balanceBoxQty
      });
      await loadAppState(false);
      showMessage(`Production updated for ${stationName}.`, "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onPlannerTableClick(event) {
    var _a, _b, _c, _d, _e, _f, _g, _h, _i, _j;
    const historyButton = event.target.closest("[data-open-history]");
    if (historyButton) {
      await openHistoryModalForOrder(Number(historyButton.dataset.openHistory));
      return;
    }
    const assignButton = event.target.closest("[data-planner-assign-station]");
    if (assignButton) {
      const orderId2 = Number(assignButton.dataset.plannerAssignStation);
      try {
        await apiPost("/api/planner/assign-station", {
          order_id: orderId2,
          station_name: ((_a = document.querySelector(`#planner-station-edit-${orderId2}`)) == null ? void 0 : _a.value) || ((_b = document.querySelector(`#planner-station-${orderId2}`)) == null ? void 0 : _b.value) || ""
        });
        await loadAppState(false);
        showMessage("Planner station updated.", "success");
      } catch (error) {
        showMessage(error.message, "error");
      }
      return;
    }
    const moveButton = event.target.closest("[data-planner-move]");
    if (moveButton) {
      try {
        await apiPost("/api/planner/move", {
          order_id: Number(moveButton.dataset.orderId),
          direction: moveButton.dataset.plannerMove
        });
        await loadAppState(false);
        showMessage("Planner sequence updated.", "success");
      } catch (error) {
        showMessage(error.message, "error");
      }
      return;
    }
    const reapproveButton = event.target.closest("[data-planner-reapprove]");
    if (reapproveButton) {
      try {
        await apiPost("/api/planner/reapprove", {
          order_id: Number(reapproveButton.dataset.plannerReapprove)
        });
        await loadAppState(false);
        showMessage("Order reapproved back to Hot Press.", "success");
      } catch (error) {
        showMessage(error.message, "error");
      }
      return;
    }
    const saveButton = event.target.closest("[data-planner-save]");
    if (!saveButton) {
      return;
    }
    const orderId = Number(saveButton.dataset.plannerSave);
    try {
      await apiPost("/api/planner/save", {
        order_id: orderId,
        sla_date: normalizePlannerDateInput(((_c = document.querySelector(`#planner-sla-edit-${orderId}`)) == null ? void 0 : _c.value) || ((_d = document.querySelector(`#planner-sla-${orderId}`)) == null ? void 0 : _d.value) || ""),
        urgency: ((_e = document.querySelector(`#planner-urgency-edit-${orderId}`)) == null ? void 0 : _e.value.trim()) || ((_f = document.querySelector(`#planner-urgency-${orderId}`)) == null ? void 0 : _f.value.trim()) || "",
        priority: ((_g = document.querySelector(`#planner-priority-edit-${orderId}`)) == null ? void 0 : _g.value.trim()) || ((_h = document.querySelector(`#planner-priority-${orderId}`)) == null ? void 0 : _h.value.trim()) || "",
        planner_remarks: ((_i = document.querySelector(`#planner-remarks-edit-${orderId}`)) == null ? void 0 : _i.value.trim()) || ((_j = document.querySelector(`#planner-remarks-${orderId}`)) == null ? void 0 : _j.value.trim()) || ""
      });
      await loadAppState(false);
      showMessage("Planner row updated.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onPlannerTableChange(event) {
    var _a, _b, _c, _d;
    const row = event.target.closest("[data-planner-order-id]");
    if (!row) return;
    if (!event.target.id || event.target.id.indexOf("planner-sla-edit-") !== 0 && event.target.id.indexOf("planner-priority-edit-") !== 0) {
      return;
    }
    const orderId = Number(row.dataset.plannerOrderId);
    const current = (((_b = (_a = state.app) == null ? void 0 : _a.planning) == null ? void 0 : _b.rows) || []).find((item) => item.order_id === orderId);
    if (!current) return;
    try {
      await apiPost("/api/planner/save", {
        order_id: orderId,
        sla_date: normalizePlannerDateInput(((_c = document.querySelector(`#planner-sla-edit-${orderId}`)) == null ? void 0 : _c.value) || current.edd || ""),
        urgency: current.urgency || "",
        priority: ((_d = document.querySelector(`#planner-priority-edit-${orderId}`)) == null ? void 0 : _d.value.trim()) || "",
        planner_remarks: current.planner_remarks || ""
      });
      await loadAppState(false);
      showMessage("Planner row updated.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  function onPlannerDragStart(event) {
    const row = event.target.closest("[data-planner-order-id]");
    if (!row) return;
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData("text/plain", row.dataset.plannerOrderId);
  }
  function onPlannerDragOver(event) {
    const row = event.target.closest("[data-planner-order-id]");
    if (!row) return;
    event.preventDefault();
    event.dataTransfer.dropEffect = "move";
  }
  async function onPlannerDrop(event) {
    var _a, _b;
    const row = event.target.closest("[data-planner-order-id]");
    if (!row) return;
    event.preventDefault();
    const draggedOrderId = Number(event.dataTransfer.getData("text/plain"));
    const targetOrderId = Number(row.dataset.plannerOrderId);
    if (!draggedOrderId || !targetOrderId || draggedOrderId === targetOrderId) return;
    const orderedRows = [...((_b = (_a = state.app) == null ? void 0 : _a.planning) == null ? void 0 : _b.rows) || []].sort((a, b) => Number(a.planning_rank || 0) - Number(b.planning_rank || 0));
    const ids = orderedRows.map((item) => item.order_id);
    const fromIndex = ids.indexOf(draggedOrderId);
    const toIndex = ids.indexOf(targetOrderId);
    if (fromIndex < 0 || toIndex < 0) return;
    ids.splice(fromIndex, 1);
    ids.splice(toIndex, 0, draggedOrderId);
    try {
      await apiPost("/api/planner/resequence", {
        ordered_ids: ids.join(",")
      });
      await loadAppState(false);
      showMessage("Planner priority updated.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  function onPlannerColumnDragStart(event) {
    const head = event.target.closest("[data-planner-column]");
    if (!head) return;
    state.ui.plannerDraggingColumn = head.dataset.plannerColumn || "";
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData("text/plain", state.ui.plannerDraggingColumn);
  }
  function onPlannerColumnDragOver(event) {
    const head = event.target.closest("[data-planner-column]");
    if (!head) return;
    event.preventDefault();
    event.dataTransfer.dropEffect = "move";
  }
  function onPlannerColumnDrop(event) {
    const head = event.target.closest("[data-planner-column]");
    if (!head) return;
    event.preventDefault();
    ensurePlannerColumnOrder();
    const dragged = state.ui.plannerDraggingColumn || event.dataTransfer.getData("text/plain");
    const target = head.dataset.plannerColumn || "";
    if (!dragged || !target || dragged === target) return;
    const order = [...state.ui.plannerColumnOrder];
    const fromIndex = order.indexOf(dragged);
    const toIndex = order.indexOf(target);
    if (fromIndex < 0 || toIndex < 0) return;
    order.splice(fromIndex, 1);
    order.splice(toIndex, 0, dragged);
    state.ui.plannerColumnOrder = order;
    savePlannerColumnOrder();
    renderPlanning();
    showMessage("Planner columns rearranged.", "success");
  }
  function onPlannerHeaderClick(event) {
    const sortButton = event.target.closest("[data-planner-sort]");
    if (!sortButton) return;
    const columnId = sortButton.dataset.plannerSort;
    if (!columnId) return;
    if (state.ui.plannerSort === columnId) {
      state.ui.plannerSortDir = state.ui.plannerSortDir === "asc" ? "desc" : "asc";
    } else {
      state.ui.plannerSort = columnId;
      state.ui.plannerSortDir = "asc";
    }
    state.ui.pagination.planner = 1;
    renderPlanning();
  }
  function onPlannerHeaderFilterChange(event) {
    const input = event.target.closest("[data-planner-filter]");
    if (!input) return;
    state.ui.plannerColumnFilters[input.dataset.plannerFilter] = input.value || "";
    state.ui.pagination.planner = 1;
    state.ui.pagination.plannerMove = 1;
    renderPlanning();
  }
  async function onSharedPlanningTableClick(event) {
    const row = event.target.closest("[data-shared-order-id]");
    if (!row) {
      return;
    }
    await openHistoryModalForOrder(Number(row.dataset.sharedOrderId));
  }
  async function onDispatchTableClick(event) {
    var _a, _b, _c, _d;
    const expandButton = event.target.closest("[data-dispatch-expand]");
    if (expandButton) {
      const orderId2 = Number(expandButton.dataset.dispatchExpand);
      state.ui.dispatchExpandedOrderId = state.ui.dispatchExpandedOrderId === orderId2 ? null : orderId2;
      renderDispatch();
      return;
    }
    const balanceSaveButton = event.target.closest("[data-dispatch-balance-save]");
    if (balanceSaveButton) {
      const orderId2 = Number(balanceSaveButton.dataset.dispatchBalanceSave);
      const balanceBoxQty2 = ((_a = document.querySelector(`#dispatch-balance-${orderId2}`)) == null ? void 0 : _a.value) || "0";
      try {
        await apiPost("/api/dispatch/balance-save", {
          order_id: orderId2,
          balance_box_qty: balanceBoxQty2
        });
        await loadAppState(false);
        showMessage("Dispatch balance saved.", "success");
      } catch (error) {
        showMessage(error.message, "error");
      }
      return;
    }
    const addBoxButton = event.target.closest("[data-dispatch-box-add]");
    if (addBoxButton) {
      try {
        await apiPost("/api/dispatch/boxes/add", { order_id: Number(addBoxButton.dataset.dispatchBoxAdd) });
        await loadAppState(false);
        state.ui.dispatchExpandedOrderId = Number(addBoxButton.dataset.dispatchBoxAdd);
        showMessage("Dispatch box added.", "success");
      } catch (error) {
        showMessage(error.message, "error");
      }
      return;
    }
    const boxButton = event.target.closest("[data-dispatch-box]");
    if (boxButton) {
      const orderId2 = Number(boxButton.dataset.dispatchBox);
      const boxNo = Number(boxButton.dataset.boxNo);
      const current = boxButton.classList.contains("dispatch-box-loaded") ? "LOADED" : boxButton.classList.contains("dispatch-box-removed") ? "REMOVED" : boxButton.classList.contains("dispatch-box-doubt") ? "DOUBT" : "NONE";
      const next = current === "LOADED" ? "DOUBT" : current === "REMOVED" ? "DOUBT" : current === "DOUBT" ? "NONE" : "LOADED";
      try {
        await apiPost("/api/dispatch/boxes/state", { order_id: orderId2, box_no: boxNo, state: next });
        await loadAppState(false);
        state.ui.dispatchExpandedOrderId = orderId2;
      } catch (error) {
        showMessage(error.message, "error");
      }
      return;
    }
    const button = event.target.closest("[data-dispatch-action]");
    if (!button) {
      return;
    }
    const orderId = button.dataset.orderId;
    const remarks = ((_b = document.querySelector(`#dispatch-remark-${orderId}`)) == null ? void 0 : _b.value.trim()) || "";
    const vehicleDetails = ((_c = document.querySelector(`#dispatch-vehicle-${orderId}`)) == null ? void 0 : _c.value.trim()) || "";
    const balanceBoxQty = ((_d = document.querySelector(`#dispatch-balance-${orderId}`)) == null ? void 0 : _d.value) || "0";
    try {
      await apiPost("/api/dispatch/action", {
        order_id: Number(orderId),
        action_code: button.dataset.dispatchAction,
        remarks,
        vehicle_details: vehicleDetails,
        balance_box_qty: balanceBoxQty
      });
      await loadAppState(false);
      showMessage("Dispatch status updated.", "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onReportsTableClick(event) {
    const button = event.target.closest("[data-lifecycle-order]");
    if (!button) {
      return;
    }
    state.ui.selectedOrderId = Number(button.dataset.lifecycleOrder);
    await loadAppState(false);
  }
  async function onHistoryTableClick(event) {
    const button = event.target.closest("[data-history-order]");
    if (!button) {
      return;
    }
    state.ui.selectedOrderId = Number(button.dataset.historyOrder);
    await loadAppState(false);
  }
  async function onPlannerMachineListClick(event) {
    var _a;
    const saveButton = event.target.closest("[data-sequence-station-save]");
    if (saveButton) {
      const sequenceItemId = Number(saveButton.dataset.sequenceStationSave);
      try {
        await apiPost("/api/sequence-profiles/update-station", {
          sequence_item_id: sequenceItemId,
          station_id: Number(((_a = document.querySelector(`#sequence-station-${sequenceItemId}`)) == null ? void 0 : _a.value) || 0)
        });
        await loadAppState(false);
        showMessage("Sequence station updated.", "success");
      } catch (error) {
        showMessage(error.message, "error");
      }
      return;
    }
    const moveButton = event.target.closest("[data-sequence-station-direction]");
    if (moveButton) {
      try {
        await apiPost("/api/sequence-profiles/reorder-station", {
          sequence_item_id: Number(moveButton.dataset.sequenceStationId),
          direction: moveButton.dataset.sequenceStationDirection
        });
        await loadAppState(false);
        showMessage("Sequence order updated.", "success");
      } catch (error) {
        showMessage(error.message, "error");
      }
      return;
    }
    const deleteButton = event.target.closest("[data-sequence-station-delete]");
    if (deleteButton) {
      try {
        await apiPost("/api/sequence-profiles/delete-station", {
          sequence_item_id: Number(deleteButton.dataset.sequenceStationDelete)
        });
        await loadAppState(false);
        showMessage("Station removed from sequence.", "success");
      } catch (error) {
        showMessage(error.message, "error");
      }
      return;
    }
    const editButton = event.target.closest("[data-machine-edit]");
    if (editButton) {
      const nextValue = window.prompt("Edit machine / station", editButton.dataset.machineName || "");
      if (!nextValue || nextValue.trim() === editButton.dataset.machineName) {
        return;
      }
      try {
        await apiPost("/api/machines/save", {
          machine_id: Number(editButton.dataset.machineId),
          machine_name: nextValue.trim()
        });
        await loadAppState(false);
        showMessage("Machine station updated.", "success");
      } catch (error) {
        showMessage(error.message, "error");
      }
      return;
    }
    await onMasterListClick(event);
  }
  async function onMasterListClick(event) {
    const reorderButton = event.target.closest("[data-master-direction]");
    if (reorderButton) {
      try {
        await apiPost("/api/masters/reorder", {
          master_name: reorderButton.dataset.masterName,
          item_id: Number(reorderButton.dataset.masterId),
          direction: reorderButton.dataset.masterDirection
        });
        await loadAppState(false);
      } catch (error) {
        showMessage(error.message, "error");
      }
      return;
    }
    const deactivateButton = event.target.closest("[data-master-deactivate]");
    if (!deactivateButton) {
      return;
    }
    try {
      await apiPost("/api/masters/deactivate", {
        master_name: deactivateButton.dataset.masterDeactivate,
        item_id: Number(deactivateButton.dataset.masterId)
      });
      await loadAppState(false);
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onDropdownMasterListClick(event) {
    const editButton = event.target.closest("[data-dropdown-edit]");
    if (editButton) {
      const nextValue = window.prompt(`Edit ${dropdownMasterLabel(editButton.dataset.dropdownMaster)}`, editButton.dataset.dropdownValue || "");
      if (!nextValue || nextValue.trim() === editButton.dataset.dropdownValue) {
        return;
      }
      try {
        await apiPost("/api/masters/dropdown-update", {
          master_name: editButton.dataset.dropdownMaster,
          item_id: Number(editButton.dataset.dropdownId),
          value: nextValue.trim()
        });
        await loadAppState(false);
        showMessage(`${dropdownMasterLabel(editButton.dataset.dropdownMaster)} updated.`, "success");
      } catch (error) {
        showMessage(error.message, "error");
      }
      return;
    }
    const deleteButton = event.target.closest("[data-dropdown-delete]");
    if (!deleteButton) {
      return;
    }
    if (!window.confirm(`Delete this ${dropdownMasterLabel(deleteButton.dataset.dropdownMaster).toLowerCase()} value?`)) {
      return;
    }
    try {
      await apiPost("/api/masters/dropdown-delete", {
        master_name: deleteButton.dataset.dropdownMaster,
        item_id: Number(deleteButton.dataset.dropdownId)
      });
      await loadAppState(false);
      showMessage(`${dropdownMasterLabel(deleteButton.dataset.dropdownMaster)} deleted.`, "success");
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  async function onUsersTableClick(event) {
    const editButton = event.target.closest("[data-user-edit]");
    if (editButton) {
      startUserEdit(Number(editButton.dataset.userEdit));
      return;
    }
    const button = event.target.closest("[data-user-toggle]");
    if (!button) {
      return;
    }
    try {
      await apiPost("/api/users/toggle", {
        user_id: Number(button.dataset.userToggle)
      });
      await loadAppState(false);
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  function onPaginationClick(event) {
    const button = event.target.closest("[data-page-key]");
    if (!button) {
      return;
    }
    const key = button.dataset.pageKey;
    const direction = Number(button.dataset.pageDirection || 0);
    state.ui.pagination[key] = Math.max(1, (state.ui.pagination[key] || 1) + direction);
    renderAll();
  }
  function syncReportFilters() {
    state.ui.reportStatus = refs.reportStatusFilter.value;
    state.ui.reportDealer = refs.reportDealerFilter.value;
    state.ui.reportOrderType = refs.reportOrderTypeFilter.value;
    state.ui.reportStation = refs.reportStationFilter.value;
    state.ui.reportDateFrom = refs.reportDateFrom.value;
    state.ui.reportDateTo = refs.reportDateTo.value;
    state.ui.reportSort = refs.reportSort.value;
    state.ui.pagination.reports = 1;
    loadAppState(false);
  }
  function applyLast7DaysFilter() {
    const today = /* @__PURE__ */ new Date();
    const start = new Date(today);
    start.setDate(today.getDate() - 6);
    state.ui.reportDateFrom = isoInputDate(start);
    state.ui.reportDateTo = isoInputDate(today);
    refs.reportDateFrom.value = state.ui.reportDateFrom;
    refs.reportDateTo.value = state.ui.reportDateTo;
    state.ui.pagination.reports = 1;
    loadAppState(false);
  }
  function syncSharedPlanningFilters() {
    state.ui.sharedPlanningSearch = refs.sharedPlanningSearch.value.trim();
    if (refs.sharedPlanningDataEntrySearch) {
      refs.sharedPlanningDataEntrySearch.value = state.ui.sharedPlanningSearch;
    }
    state.ui.sharedPlanningStage = refs.sharedPlanningStage.value;
    state.ui.sharedPlanningSort = refs.sharedPlanningSort.value;
    state.ui.pagination.sharedPlanningProduction = 1;
    renderProduction();
  }
  function syncSharedPlanningDataEntryFilters() {
    state.ui.sharedPlanningSearch = refs.sharedPlanningDataEntrySearch.value.trim();
    if (refs.sharedPlanningSearch) {
      refs.sharedPlanningSearch.value = state.ui.sharedPlanningSearch;
    }
    state.ui.pagination.sharedPlanningDataEntry = 1;
    renderDataEntry();
  }
  function exportReportCsv() {
    if (!state.app || !state.app.reports.rows.length) {
      showMessage("No report rows available for export.", "error");
      return;
    }
    const rows = state.app.reports.rows;
    const labels = ["Order Number", "Dealer", "Customer", "Order Type", "Workflow Stage", "Visible Stations", "Last Action", "Updated", "Dispatch Status"];
    const lines = [
      labels.join(","),
      ...rows.map((row) => [
        csvValue(row.order_number || ""),
        csvValue(row.dealer_name || ""),
        csvValue(customerLabel(row.customer_name)),
        csvValue(row.order_type || ""),
        csvValue(row.workflow_stage || ""),
        csvValue(row.visible_stations || ""),
        csvValue(row.last_action || ""),
        csvValue(row.updated_at || ""),
        csvValue(row.dispatch_status || "")
      ].join(","))
    ];
    downloadCsv("elenza-pms-report.csv", lines.join("\n"));
  }
  function exportSharedPlanningExcel() {
    const rows = getSharedPlanningRows();
    if (!rows.length) {
      showMessage("No shared planning rows available for export.", "error");
      return;
    }
    const data = rows.map((row) => ({
      order_number: row.order_number,
      dealer_name: row.dealer_name,
      customer_name: customerLabel(row.customer_name),
      stage: row.planner_stage_label,
      sla_date: row.sla_date || "",
      urgency: row.urgency || "",
      priority: row.priority || "",
      visible_stations: row.visible_stations,
      planner_remarks: row.planner_remarks || "",
      partial_pending: row.partial_pending ? "Yes" : "No",
      assigned_station: row.assigned_station || ""
    }));
    const worksheet = XLSX.utils.json_to_sheet(data);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, "Shared Planning");
    XLSX.writeFile(workbook, "elenza-shared-production-view.xlsx");
  }
  function exportOptimisationExcel() {
    if (!window.XLSX) {
      showMessage("Excel export library did not load.", "error");
      return;
    }
    const rows = getOptimisationRows();
    if (!rows.length) {
      showMessage("No optimisation rows available for export.", "error");
      return;
    }
    const data = rows.map((row) => ({
      confirmation_date: row.confirmation_date || "",
      order_number: row.order_number || "",
      dealer_name: row.dealer_name || "",
      customer_name: customerLabel(row.customer_name),
      order_type: row.order_type || ""
    }));
    const worksheet = XLSX.utils.json_to_sheet(data);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, "Optimisation");
    XLSX.writeFile(workbook, "elenza-pending-optimisation-orders.xlsx");
  }
  function exportPlannerExcel() {
    const rows = getPlannerRows();
    if (!rows.length) {
      showMessage("No planner rows available for export.", "error");
      return;
    }
    const data = rows.map((row) => ({
      confirmation_date: row.confirmation_date || "",
      order_number: row.order_number,
      customer_name: customerLabel(row.customer_name),
      customer_type: row.customer_type || "",
      order_type: row.order_type,
      order_class: row.order_class || "",
      material_received_date: row.material_received_date || "",
      current_status: row.planner_stage_label || "",
      edd: row.edd || "",
      panel_qty: row.panel_qty || "",
      board_qty: row.board_qty || "",
      priority: row.priority || ""
    }));
    const worksheet = XLSX.utils.json_to_sheet(data);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, "Planner Queue");
    XLSX.writeFile(workbook, "elenza-planner-queue.xlsx");
  }
  function exportPlannerMoveExcel() {
    const rows = getPlannerRows();
    if (!rows.length) {
      showMessage("No move-order rows available for export.", "error");
      return;
    }
    const data = rows.map((row) => ({
      order_number: row.order_number || "",
      dealer_name: row.dealer_name || "",
      customer_name: customerLabel(row.customer_name),
      current_visible_station: row.assigned_station || row.current_stage_hint || "",
      order_type: row.order_type || "",
      current_status: row.current_stage_hint || row.planner_stage_label || ""
    }));
    const worksheet = XLSX.utils.json_to_sheet(data);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, "Move Orders");
    XLSX.writeFile(workbook, "elenza-planner-move-orders.xlsx");
  }
  async function apiGet(path, requireAuth, query = {}) {
    const response = await fetchJson(apiUrl(path, query), {
      credentials: "same-origin"
    });
    const result = await parseApiResult(response);
    if (!response.ok) {
      throw new Error(result.message || "Request failed.");
    }
    if (requireAuth && !result.session && result.authenticated === false) {
      throw new Error("Login required.");
    }
    return result;
  }
  async function apiPost(path, payload) {
    const params = new URLSearchParams();
    Object.entries(payload).forEach(([key, value]) => {
      params.append(key, value != null ? value : "");
    });
    const response = await fetchJson(apiUrl(path), {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
      body: params.toString()
    });
    const result = await parseApiResult(response);
    if (!response.ok) {
      throw new Error(result.message || "Request failed.");
    }
    return result;
  }
  function apiUrl(path, query = {}) {
    const url = new URL("/api.ashx", window.location.origin);
    url.searchParams.set("action", API_ACTIONS[path] || path);
    Object.entries(query).forEach(([key, value]) => {
      if (value !== void 0 && value !== null && String(value) !== "") {
        url.searchParams.set(key, value);
      }
    });
    return url.toString();
  }
  async function buildImportRowsTsv(file) {
    const workbook = XLSX.read(await file.arrayBuffer(), { type: "array" });
    const firstSheet = workbook.Sheets[workbook.SheetNames[0]];
    const rawRows = XLSX.utils.sheet_to_json(firstSheet, { defval: "" });
    const headers = ["full_name", "login_id", "role_name", "assigned_station", "is_active", "password"];
    const lines = [headers.join("	")];
    for (const row of rawRows) {
      const normalized = normalizeImportRow(row);
      if (!normalized.full_name || !normalized.login_id || !normalized.role_name) {
        continue;
      }
      const line = [
        normalized.full_name,
        normalized.login_id.toLowerCase(),
        normalized.role_name,
        normalized.assigned_station,
        normalized.is_active ? "true" : "false",
        normalized.password
      ].map(tsvCell).join("	");
      lines.push(line);
    }
    return lines.join("\n");
  }
  async function buildDealerImportRowsTsv(file) {
    var _a;
    const workbook = XLSX.read(await file.arrayBuffer(), { type: "array" });
    const firstSheet = workbook.Sheets[workbook.SheetNames[0]];
    const headers = ["dealer_code", "dealer_name", "company_name", "dealer_type", "customer_type", "city", "pin_code", "gst_number", "contact_person", "mobile_number", "email", "payment_terms", "credit_limit_lakh", "marketing_owner", "address"];
    const rawRows = worksheetRowsToObjects(firstSheet, ["dealer_name", "dealer name", "dealer", "mobile_number", "mobile", "phone"], headers);
    const lines = [headers.join("	")];
    const marketingOwnerOverride = isMarketingUserSession() ? ((_a = state.session) == null ? void 0 : _a.full_name) || "" : "";
    for (const row of rawRows) {
      const normalized = normalizeDealerImportRow(row);
      if (!normalized.dealer_name || !normalized.dealer_type || !normalized.customer_type || !normalized.mobile_number) {
        continue;
      }
      lines.push(headers.map((header) => {
        if (header === "marketing_owner" && marketingOwnerOverride) {
          return tsvCell(marketingOwnerOverride);
        }
        return tsvCell(normalized[header] || "");
      }).join("	"));
    }
    return lines.join("\n");
  }
  async function buildQuotationImportRowsTsv(file) {
    const workbook = XLSX.read(await file.arrayBuffer(), { type: "array" });
    const firstSheet = workbook.Sheets[workbook.SheetNames[0]];
    const headers = ["dealer_name", "customer_name", "order_type", "order_class", "main_order_reference", "order_number", "approx_value", "remarks"];
    const rawRows = worksheetRowsToObjects(firstSheet, ["dealer_name", "dealer name", "dealer", "order_number", "order number"], headers);
    const lines = [headers.join("	")];
    for (const row of rawRows) {
      const normalized = normalizeQuotationImportRow(row);
      if (!normalized.dealer_name || !normalized.order_type || !normalized.order_class || !normalized.order_number) {
        continue;
      }
      lines.push(headers.map((header) => tsvCell(normalized[header] || "")).join("	"));
    }
    return lines.join("\n");
  }
  function worksheetRowsToObjects(sheet, requiredHeaderHints, fallbackHeaders) {
    const rows = XLSX.utils.sheet_to_json(sheet, { header: 1, defval: "" });
    const headerIndex = rows.findIndex((row) => {
      const keys = row.map((cell) => normalizeImportKey(cell));
      return requiredHeaderHints.some((hint) => keys.includes(normalizeImportKey(hint)));
    });
    if (headerIndex < 0) {
      return rows.filter((row) => row.some((cell) => String(cell || "").trim() !== "")).map((row) => {
        const obj = {};
        fallbackHeaders.forEach((header, index) => {
          var _a;
          obj[header] = (_a = row[index]) != null ? _a : "";
        });
        return obj;
      });
    }
    const headers = rows[headerIndex].map((cell) => String(cell || "").trim());
    return rows.slice(headerIndex + 1).map((row) => {
      const obj = {};
      headers.forEach((header, index) => {
        var _a;
        if (header) obj[header] = (_a = row[index]) != null ? _a : "";
      });
      return obj;
    });
  }
  function colorMandatoryHeaders(sheet, headers, mandatoryHeaders) {
    headers.forEach((header, index) => {
      if (!mandatoryHeaders.includes(header)) return;
      const address = XLSX.utils.encode_cell({ r: 0, c: index });
      if (!sheet[address]) return;
      sheet[address].s = {
        fill: { patternType: "solid", fgColor: { rgb: "C6EFCE" } },
        font: { bold: true, color: { rgb: "006100" } }
      };
    });
  }
  function normalizeImportRow(row) {
    const read = (...keys) => readImportCell(row, ...keys);
    const activeValue = read("is_active", "active") || "true";
    return {
      full_name: read("full_name", "name"),
      login_id: read("login_id", "login", "username"),
      password: read("password") || "demo123",
      role_name: read("role_name", "role"),
      assigned_station: read("assigned_station", "station"),
      is_active: ["true", "1", "yes", "active"].includes(activeValue.toLowerCase())
    };
  }
  function normalizeDealerImportRow(row) {
    const read = (...keys) => readImportCell(row, ...keys);
    return {
      dealer_code: read("dealer_code", "dealer id", "dealer id no.", "dealer id no", "dealer_id"),
      dealer_name: read("dealer_name", "dealer name", "dealer"),
      company_name: read("company_name", "company name", "company"),
      dealer_type: read("dealer_type", "dealer type"),
      customer_type: read("customer_type", "customer type"),
      city: read("city"),
      pin_code: read("pin_code", "pin code", "pincode"),
      gst_number: read("gst_number", "gst number", "gst"),
      contact_person: read("contact_person", "contact person", "contact"),
      mobile_number: read("mobile_number", "mobile", "phone"),
      email: read("email"),
      payment_terms: read("payment_terms", "payment terms"),
      credit_limit_lakh: read("credit_limit_lakh", "credit limit (lakh)"),
      marketing_owner: read("marketing_owner", "marketing owner"),
      address: read("address")
    };
  }
  function normalizeQuotationImportRow(row) {
    const read = (...keys) => readImportCell(row, ...keys);
    return {
      dealer_name: read("dealer_name", "dealer name", "dealer"),
      customer_name: read("customer_name", "customer name"),
      order_type: read("order_type", "order type"),
      order_class: read("order_class", "order class", "main / sub / snag / rework", "main/sub/snag/rework"),
      main_order_reference: read("main_order_reference", "main order reference", "sub_order", "sub order", "main_order_ref"),
      order_number: read("order_number", "order number", "order no", "order no."),
      approx_value: read("approx_value", "approx value", "value"),
      remarks: read("remarks", "remark")
    };
  }
  function readImportCell(row, ...keys) {
    const normalizedRow = {};
    Object.keys(row).forEach((key) => {
      normalizedRow[normalizeImportKey(key)] = row[key];
    });
    for (const key of keys) {
      const value = normalizedRow[normalizeImportKey(key)];
      if (value !== void 0 && value !== null && String(value).trim() !== "") {
        return String(value).trim();
      }
    }
    return "";
  }
  function normalizeImportKey(value) {
    return String(value || "").toLowerCase().replace(/[^a-z0-9]+/g, "");
  }
  function tsvCell(value) {
    return String(value != null ? value : "").split("	").join(" ").split("\r").join(" ").split("\n").join(" ");
  }
  async function openHistoryModalForOrder(orderId) {
    state.ui.selectedOrderId = orderId;
    state.ui.historyModalOpen = true;
    await loadAppState(false);
  }
  function closeHistoryModal() {
    state.ui.historyModalOpen = false;
    refs.historyModal.classList.add("hidden");
  }
  async function onGlobalDblClick(event) {
    const boxButton = event.target.closest("[data-dispatch-box]");
    if (!boxButton) {
      return;
    }
    event.preventDefault();
    const orderId = Number(boxButton.dataset.dispatchBox);
    const boxNo = Number(boxButton.dataset.boxNo);
    try {
      await apiPost("/api/dispatch/boxes/state", { order_id: orderId, box_no: boxNo, state: "REMOVED" });
      await loadAppState(false);
      state.ui.dispatchExpandedOrderId = orderId;
    } catch (error) {
      showMessage(error.message, "error");
    }
  }
  function onGlobalKeydown(event) {
    if (event.key === "Escape" && state.ui.historyModalOpen) {
      closeHistoryModal();
    }
  }
  function fillDatalist(target, values) {
    target.innerHTML = values.filter(Boolean).map((value) => `<option value="${escapeAttribute(value)}"></option>`).join("");
  }
  function fillSelect(target, options, selectedValue) {
    target.innerHTML = options.map((option) => {
      const value = typeof option === "string" ? option : option.value;
      const label = typeof option === "string" ? option : option.label;
      return `<option value="${escapeAttribute(value)}" ${value === selectedValue ? "selected" : ""}>${escapeHtml(label)}</option>`;
    }).join("");
  }
  function pill(text) {
    const value = String(text || "-");
    const lower = value.toLowerCase();
    const tone = lower.includes("reject") || lower.includes("hold") ? "red" : lower.includes("dispatch") || lower.includes("complete") ? "green" : lower.includes("partial") || lower.includes("pending") ? "orange" : "blue";
    return `<span class="pill ${tone}">${escapeHtml(value)}</span>`;
  }
  function uniqueValues(values) {
    return [...new Set(values.filter(Boolean))];
  }
  function detailPlaceholder(text) {
    return `
    <div class="detail-card">
      <strong>Lifecycle</strong>
      <div class="detail-line">${escapeHtml(text)}</div>
    </div>
  `;
  }
  function metricCard(label, value, footnote) {
    return `
    <div class="metric-card">
      <span class="metric-label">${escapeHtml(label)}</span>
      <span class="metric-value">${escapeHtml(value)}</span>
      <span class="metric-footnote">${escapeHtml(footnote)}</span>
    </div>
  `;
  }
  function setFormDisabled(form, disabled, placeholderText) {
    form.querySelectorAll("input, select, textarea, button").forEach((element) => {
      element.disabled = disabled;
    });
    const orderInput = form.querySelector("input[list]");
    if (orderInput) {
      orderInput.placeholder = disabled ? placeholderText : "";
    }
  }
  function isoInputDate(value) {
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, "0");
    const day = String(value.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
  }
  function clearTable(target, columns, message) {
    target.innerHTML = emptyRow(columns, message);
  }
  function emptyRow(columns, message) {
    return `<tr><td colspan="${columns}">${escapeHtml(message)}</td></tr>`;
  }
  function showMessage(message, type) {
    const target = state.session ? refs.messageStrip : refs.authMessageStrip;
    const other = state.session ? refs.authMessageStrip : refs.messageStrip;
    other.classList.add("hidden");
    target.textContent = message;
    target.classList.remove("hidden", "error", "success");
    target.classList.add(type === "error" ? "error" : "success");
  }
  function renderLocalOnlyMessage() {
    renderLoggedOut();
    refs.loginForm.querySelector(".auth-submit").disabled = true;
    refs.loginUsername.disabled = true;
    refs.loginPassword.disabled = true;
    showMessage("Open the PMS through a web server URL. Do not open index.html directly.", "error");
  }
  async function fetchJson(url, options) {
    try {
      return await fetch(url, options);
    } catch (e) {
      throw new Error(transportErrorMessage());
    }
  }
  async function parseApiResult(response) {
    const raw = await response.text();
    if (!raw) {
      return {};
    }
    try {
      return JSON.parse(raw);
    } catch (e) {
      if (looksLikeHtml(raw)) {
        throw new Error(htmlResponseMessage(response.url));
      }
      throw new Error("Server returned an invalid response.");
    }
  }
  function looksLikeHtml(text) {
    const value = String(text || "").trimStart().toLowerCase();
    return value.startsWith("<!doctype") || value.startsWith("<html") || value.startsWith("<head") || value.startsWith("<body");
  }
  function htmlResponseMessage(url) {
    if (FILE_MODE) {
      return "Open the PMS through a web server URL. Do not open index.html directly.";
    }
    return `This host returned an HTML page instead of PMS API data for ${url}. The frontend is loading, but the backend API is not available on this server path.`;
  }
  function transportErrorMessage() {
    if (FILE_MODE) {
      return "Open the PMS through a web server URL. Do not open index.html directly.";
    }
    return "Server is not reachable. Refresh once the site backend is available.";
  }
  function debounce(fn, delay) {
    let timer = null;
    return (...args) => {
      window.clearTimeout(timer);
      timer = window.setTimeout(() => fn(...args), delay);
    };
  }
  function downloadCsv(filename, content) {
    const blob = new Blob([content], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }
  function csvValue(value) {
    return `"${String(value).split('"').join('""')}"`;
  }
  function safeId(value) {
    return String(value || "").toLowerCase().replace(/[^a-z0-9]+/g, "-");
  }
  function customerLabel(value) {
    const customer = String(value || "").trim();
    return customer ? `(${customer})` : "-";
  }
  function dealerWithCustomer(dealer, customer) {
    const dealerName = String(dealer || "").trim() || "-";
    const customerText = String(customer || "").trim();
    return customerText ? `${dealerName} (${customerText})` : dealerName;
  }
  function escapeHtml(value) {
    return String(value != null ? value : "").split("&").join("&amp;").split("<").join("&lt;").split(">").join("&gt;").split('"').join("&quot;").split("'").join("&#39;");
  }
  function escapeAttribute(value) {
    return escapeHtml(value);
  }
})();
