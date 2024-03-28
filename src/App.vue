<template>
    <Splitter class="splitter flex-grow-1">
        <SplitterPanel class="panel" :size="25" :minSize="20">
            <div class="flex">
                <div class="flex-grow-1">
                    <h2 class="mb-0 ml-4 mt-3">Saves</h2>
                </div>

                <div class="flex justify-content-center">
                    <Button class="tree-title-button" icon="pi pi-plus" severity="secondary" text aria-label="Add" 
                            size="small" @click.stop="addSave()"
                            v-tooltip.left="{ value: 'Add Save', showDelay: 1000 }" />
                </div>
            </div>

            <div class="tree-wrapper">
                <Tree class="w-full h-full bg-transparent text-400" :value="nodes" :filter="true" filterMode="lenient"
                      filterPlaceholder="Search saves..." scrollHeight="flex" :metaKeySelection="false" selectionMode="single"
                       v-model:selectionKeys="selectedKeys" @node-select="onNodeSelected" @node-unselect="onNodeUnselected">
                    <template #default="slotProps">
                        <div class="tree-node">
                            <div class="tree-node-label">
                                {{ slotProps.node.label }}
                            </div>
                        </div>
                    </template>
                </Tree>
            </div>
        </SplitterPanel>

        <SplitterPanel class="panel" :size="75" :minSize="20">
            <SaveGame v-if="selectedId" :id="selectedId" @cancelled="onCancelled" @deleted="onDeleted" @saved="onSaved" @changed="onChanged" />
            <div v-else class="flex flex-column flex-grow-1 justify-content-center">
                <span class="text-center text-color-secondary">Select or create a save to get started.</span>
            </div>
        </SplitterPanel>
    </Splitter>

    <Toast position="top-right" group="tr" />
    <ConfirmDialog></ConfirmDialog>
</template>

<script setup>
import { ref, onBeforeMount } from "vue";
import SaveGame from "./Components/SaveGame.vue";
import { useConfirm } from "primevue/useconfirm";

const nodes = ref(null);
const selectedKeys = ref({});
const selectedId = ref(null);
const hasUnsavedChanges = ref(false);

const confirm = useConfirm();

onBeforeMount(async () => {
    await getSaves();
});

async function onNodeSelected(node) {
    if (node.type !== "game") {
        if (hasUnsavedChanges.value) {
            confirmChangeSelected(node.key);
        } else {
            selectedId.value = node.key;
        }
    }
}

async function onNodeUnselected(node) {
    if (node.type !== "game") {
        if (hasUnsavedChanges.value) {
            confirmChangeSelected(null);
        } else {
            selectedId.value = null;
        }
    }
}

async function addSave() {
    selectedId.value = "new";
    selectedKeys.value = null;
}

async function onCancelled() {
    selectedId.value = null;
    selectedKeys.value = null;
    hasUnsavedChanges.value = false;
}

async function onDeleted() {
    selectedId.value = null;
    selectedKeys.value = null;
    hasUnsavedChanges.value = false;
    await getSaves();
}

async function onSaved(e) {
    selectedId.value = e.Id;
    selectedKeys.value = { [e.Id]: true };
    hasUnsavedChanges.value = false;
    await getSaves();
}

async function onChanged() {
    hasUnsavedChanges.value = true;
}

async function getSaves() {
    nodes.value = await galdrInvoke("getSaves");
}

function confirmChangeSelected(key) {
    confirm.require({
        message: 'There are unsaved changes. Continue?',
        header: 'Unsaved Changes',
        icon: 'pi pi-info-circle',
        rejectLabel: 'Cancel',
        acceptLabel: 'Yes',
        rejectClass: 'p-button-secondary p-button-outlined',
        acceptClass: 'p-button-danger',
        accept: async () => {
            hasUnsavedChanges.value = false;
            selectedKeys.value = { [key]: true };
            selectedId.value = key;
        },
        reject: () => {
            selectedKeys.value = { [selectedId.value]: true };
        }
    });
}
</script>

<style>
html, body {
    margin: 0;
    padding: 0;
    height: 100%;
    display: flex;
    flex-direction: column;
    overflow: hidden;
}

#app {
    display: flex;
    flex-grow: 1;
}

.p-tree .p-tree-container .p-treenode:focus > .p-treenode-content {
    outline: none;
}

.p-tree-container {
    overflow-x: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
}

.p-treenode-label {
    display: flex;
    flex: 1 1 1px;
    max-width: 100%;
    overflow-x: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
}

::-webkit-scrollbar {
  width: 9px;
}

::-webkit-scrollbar-track {
  border-radius: 4px;
}

::-webkit-scrollbar-thumb {
  background: var(--surface-300);
  border-radius: 4px;
}
</style>

<style scoped>
.splitter {
    background-color: transparent;
    border: none;
    display: flex;
    height: 100%;
    max-height: 100;
}

.panel {
    display: flex;
    flex-direction: column;
}

.tree-wrapper {
    overflow-y: hidden;
    flex: 1 1 1px;
    width: 100%;
    display: flex;
    flex-direction: column;
}

.tree-title-button {
    font-size: 0.65rem;
    padding: 0.3rem 0.3rem;
    width: 2rem;
    margin-right: 1em;
    margin-top: 1.85em;
}

.tree-node {
    display: flex;
    flex-grow: 1;
    flex: 1 1 1px;
    width: 100%;
}

.tree-node-label {
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}
</style>