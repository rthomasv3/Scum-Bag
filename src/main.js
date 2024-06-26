﻿import { createApp } from "vue";
import App from "./App.vue";
import PrimeVue from "primevue/config";
import ConfirmationService from "primevue/confirmationservice";
import ToastService from "primevue/toastservice";
import ConfirmDialog from "primevue/confirmdialog";
import Toast from "primevue/toast";
import Tooltip from "primevue/tooltip";
import Button from "primevue/button";
import Tree from "primevue/tree";
import InputText from "primevue/inputtext";
import Splitter from "primevue/splitter";
import SplitterPanel from "primevue/splitterpanel";
import InputNumber from "primevue/inputnumber";
import AutoComplete from "primevue/autocomplete";
import Listbox from "primevue/listbox";
import InputSwitch from "primevue/inputswitch";
import Image from "primevue/image";
import Dialog from "primevue/dialog";
import Checkbox from "primevue/checkbox";
import ConfirmPopup from "primevue/confirmpopup";
import Dropdown from "primevue/dropdown";
import "primeflex/primeflex.css";
import "primeicons/primeicons.css";

const app = createApp(App);

app.use(PrimeVue);
app.use(ConfirmationService);
app.use(ToastService);
app.directive("tooltip", Tooltip);

app.component("ConfirmDialog", ConfirmDialog);
app.component("Toast", Toast);
app.component("Button", Button);
app.component("Tree", Tree);
app.component("InputText", InputText);
app.component("Splitter", Splitter);
app.component("SplitterPanel", SplitterPanel);
app.component("InputNumber", InputNumber);
app.component("AutoComplete", AutoComplete);
app.component("Listbox", Listbox);
app.component("InputSwitch", InputSwitch);
app.component("Image", Image);
app.component("Dialog", Dialog);
app.component("Checkbox", Checkbox);
app.component("ConfirmPopup", ConfirmPopup);
app.component("Dropdown", Dropdown);

app.mount("#app");
