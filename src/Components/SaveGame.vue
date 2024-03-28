<template>
    <div class="scrollable ml-4 mr-2 mt-3 mb-3 pr-3 flex flex-grow-1 flex-column">
        <div class="flex">
            <h2 class="mt-0 flex-grow-1">Details</h2>

            <InputSwitch v-model="enabled" v-tooltip.left="{ value: 'Enabled', showDelay: 1000 }" />
        </div>

        <div class="mt-1 flex flex-column gap-4">
            <div class="flex flex-column gap-2">
                <label for="name">Name</label>
                <InputText id="name" placeholder="Enter a name..." v-model="name" />
            </div>

            <div class="flex flex-column gap-2">
                <label for="location">Save Location</label>
                <div class="flex gap-3">
                    <InputText id="location" class="flex-grow-1" placeholder="Save location to backup..." v-model="location" />
                    <Button label="File" icon="pi pi-file" @click="openFile" />
                    <Button label="Directory" icon="pi pi-folder-open" @click="openDirectory" />
                </div>
            </div>

            <div v-if="backupLocation" class="flex flex-column gap-2">
                <label for="backup-location">Backup Location</label>
                <div class="flex gap-3">
                    <InputText id="backup-location" class="flex-grow-1" placeholder="Save location to backup..." v-model="backupLocation" variant="filled" readonly />
                    <Button icon="pi pi-copy" severity="secondary" text @click="copyPath(backupLocation)" />
                </div>
            </div>

            <div class="flex flex-column gap-2 p-fluid">
                <label for="game">Game</label>
                <AutoComplete v-model="game" :suggestions="filteredGames" placeholder="Game name..."  @complete="search" />
            </div>

            <div>
                <div class="formgrid grid">
                    <div class="field col">
                        <div class="flex flex-column gap-2">
                            <label for="frequency">Backup Frequency (mins)</label>
                            <InputNumber class="flex-grow-1" v-model="frequency" inputId="frequency" showButtons buttonLayout="horizontal" :step="1" :min="1" :max="60">
                                <template #incrementbuttonicon>
                                    <span class="pi pi-plus" />
                                </template>
                                <template #decrementbuttonicon>
                                    <span class="pi pi-minus" />
                                </template>
                            </InputNumber>
                        </div>
                    </div>

                    <div class="field col">
                        <div class="flex flex-column gap-2">
                            <label for="max-backups">Max Backups</label>
                            <InputNumber v-model="max" inputId="max-backups" showButtons buttonLayout="horizontal" :step="1" :min="1" :max="60">
                                <template #incrementbuttonicon>
                                    <span class="pi pi-plus" />
                                </template>
                                <template #decrementbuttonicon>
                                    <span class="pi pi-minus" />
                                </template>
                            </InputNumber>
                        </div>
                    </div>
                </div>

                <div class="text-color-secondary">{{ backupDisplay }} total mins of backups</div>
            </div>

            <div class="flex mt-2 gap-2 justify-content-end">
                <Button v-if="props.id !== 'new'" label="Delete" severity="danger" @click="deleteSave" />
                <Button v-else label="Cancel" severity="secondary" @click="cancel" />
                <Button label="Save Details" @click="save" :disabled="!hasChanges" />
            </div>
        </div>

        <div v-if="props.id !== 'new'" class=" mt-4 border-top-1 surface-border">
            <h2 class="">Backups</h2>

            <div class="flex flex-column gap-2">
                <div class="grid">
                    <div class="col-fixed" style="width:275px; height:265px">
                        <div class="flex flex-wrap justify-content-center align-content-center border-round border-1 border-200 h-full w-full">
                            <Image v-if="screenshot" :src="screenshot" alt="Image" width="250" preview />
                            <div v-else>
                                No Screenshot
                            </div>
                        </div>
                    </div>

                    <div class="col">
                        <Listbox v-model="selectedBackup" :options="backups" listStyle="height:250px" emptyMessage="No Backups">
                            <template #option="slotProps">
                                <div v-if="backupLocation" class="flex gap-3 backup-option align-items-center">
                                    <div class="flex-grow-1">{{ new Date(slotProps.option.Time).toLocaleString() }}</div>
                                    <Button class="backup-copy tiny-button" icon="pi pi-copy" severity="secondary" text @click.stop="copyPath(slotProps.option.Directory)" />
                                </div>
                            </template>
                        </Listbox>
                    </div>
                </div>

                <div class="flex gap-3">
                    <div class="flex flex-grow-1">
                        <Button label="Backup Now" @click="manualBackup" />
                    </div>

                    <div class="flex">
                        <Button label="Restore" :disabled="!selectedBackup" @click="restore" />
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, computed, onBeforeMount, watch } from "vue";
import { useToast } from 'primevue/usetoast';
import { useConfirm } from "primevue/useconfirm";

const props = defineProps(["id"]);
const emit = defineEmits(["cancelled", "deleted", "saved", "changed"]);

const name = ref(null);
const enabled = ref(true);
const backupLocation = ref(null);
const game = ref("");
const allGames = ref([]);
const filteredGames = ref([]);
const location = ref(null);
const frequency = ref(5);
const max = ref(24);
const backupDisplay = computed(() => {
    return frequency.value * max.value;
});
const backups = ref([]);
const selectedBackup = ref(null);
const hasChanges = ref(false);
const screenshot = computed(() => {
    if (selectedBackup.value?.Screenshot) {
        return "data:image/jpg;base64," + selectedBackup.value.Screenshot;
    } else {
        return null;
    }
});

const toast = useToast();
const confirm = useConfirm();

var supressChangeFlag = false;

const search = async (event) => {
    filteredGames.value = allGames.value.filter((game) => {
        return game.toLowerCase().indexOf(event.query.toLowerCase()) > -1;
    });
}

onBeforeMount(async () => {
    supressChangeFlag = true;
    await getSave();
    await getBackups();
    selectedBackup.value = null;
    allGames.value = await galdrInvoke("getGames");
    supressChangeFlag = false;

    window.addEventListener(
        "saveUpdated",
        async (e) => {
            if (e.detail && e.detail.Id === props.id) {
                await getBackups();
            }
        },
        false,
    );
});

watch(() => props.id, async () => {
    supressChangeFlag = true;
    await getSave();
    await getBackups();
    selectedBackup.value = null;
    hasChanges.value = false;
    supressChangeFlag = false;

}, { flush: 'post' });

watch(enabled, () => {
    if (!supressChangeFlag) {
        hasChanges.value = true;
        emit("changed");
    }
});

watch(name, () => {
    if (!supressChangeFlag) {
        hasChanges.value = true;
        emit("changed");
    }
});

watch(location, () => {
    if (!supressChangeFlag) {
        hasChanges.value = true;
        emit("changed");
    }
});

watch(game, () => {
    if (!supressChangeFlag) {
        hasChanges.value = true;
        emit("changed");
    }
});

watch(frequency, () => {
    if (!supressChangeFlag) {
        hasChanges.value = true;
        emit("changed");
    }
});

watch(max, () => {
    if (!supressChangeFlag) {
        hasChanges.value = true;
        emit("changed");
    }
});

async function getSave() {
    if (props.id !== "new") {
        const saveGame = await galdrInvoke("getSave", { id: props.id });
        if (saveGame) {
            name.value = saveGame.Name;
            enabled.value = saveGame.Enabled;
            backupLocation.value = saveGame.BackupLocation;
            game.value = saveGame.Game;
            location.value = saveGame.SaveLocation;
            frequency.value = saveGame.Frequency;
            max.value = saveGame.MaxBackups;
        }
    } else {
        name.value = null;
        enabled.value = true;
        backupLocation.value = null;
        game.value = "";
        location.value = null;
        frequency.value = 5;
        max.value = 24;
    }
}

async function getBackups() {
    if (props.id !== "new") {
        backups.value = await galdrInvoke("getBackups", { id: props.id });
    } else {
        backups.value = [];
    }
}

async function openFile() {
    location.value = await galdrInvoke("openFileDialog");
}

async function openDirectory() {
    location.value = await galdrInvoke("openDirectoryDialog");
}

function cancel() {
    emit("cancelled");
}

function deleteSave() {
    confirm.require({
        message: 'Do you want to delete this entry?',
        header: 'Delete Save',
        icon: 'pi pi-info-circle',
        rejectLabel: 'Cancel',
        acceptLabel: 'Delete',
        rejectClass: 'p-button-secondary p-button-outlined',
        acceptClass: 'p-button-danger',
        accept: async () => {
            const deleted = await galdrInvoke("deleteSave", { Id: props.id });
            if (deleted) {
                toast.add({ severity: 'success', summary: 'Success', detail: 'Deleted successfully', group: 'tr', life: 3000 });
                emit("deleted");
            } else {
                toast.add({ severity: 'error', summary: 'Failed', detail: 'Deletion failed', group: 'tr', life: 3000 });
            }
        },
        reject: () => {
            
        }
    });
}

async function save() {
    var success = false;
    
    if (props.id === "new") {
        let saveGame = {
            Id: uuidv4(),
            Name: name.value,
            Enabled: enabled.value,
            SaveLocation: location.value,
            Game: game.value,
            Frequency: frequency.value,
            MaxBackups: max.value
        };
        const id = await galdrInvoke("createSave", saveGame);

        if (id) {
            success = true;
            saveGame.Id = id;
            emit("saved", saveGame);
        }
    } else {
        let saveGame = {
            Id: props.id,
            Name: name.value,
            Enabled: enabled.value,
            BackupLocation: backupLocation.value,
            SaveLocation: location.value,
            Game: game.value,
            Frequency: frequency.value,
            MaxBackups: max.value
        };
        success = await galdrInvoke("updateSave", saveGame);
        if (success) {
            emit("saved", saveGame);
        }
    }

    if (success) {
        hasChanges.value = false;
        toast.add({ severity: 'success', summary: 'Success', detail: 'Saved successfully', group: 'tr', life: 3000 });
    } else {
        toast.add({ severity: 'error', summary: 'Failed', detail: 'Save failed', group: 'tr', life: 3000 });
    }
}

function uuidv4() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'
    .replace(/[xy]/g, function (c) {
        const r = Math.random() * 16 | 0, 
            v = c == 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

async function manualBackup() {
    const backedUp = await galdrInvoke("createManualBackup", { Id: props.id });

    if (backedUp) {
        toast.add({ severity: 'success', summary: 'Success', detail: 'Backup created successfully', group: 'tr', life: 3000 });
    } else {
        toast.add({ severity: 'error', summary: 'Failed', detail: 'Backups are already up-to-date', group: 'tr', life: 3000 });
    }
}

async function restore() {
    const restored = await galdrInvoke("restore", { Id: props.id, Time: selectedBackup.value.Time });

    if (restored) {
        toast.add({ severity: 'success', summary: 'Success', detail: 'Save restored successfully', group: 'tr', life: 3000 });
    } else {
        toast.add({ severity: 'error', summary: 'Failed', detail: 'Save restore failed (permissions error)', group: 'tr', life: 3000 });
    }
}

function copyPath(path) {
    navigator.clipboard.writeText(path).then(function() {
        toast.add({ severity: 'info', summary: 'Copied', detail: 'Copied path to clipboard', group: 'tr', life: 3000 });
    }, function(err) {
        toast.add({ severity: 'error', summary: 'Failed', detail: 'Failed to copy path to clipboard', group: 'tr', life: 3000 });
    });
}
</script>

<style scoped>
.scrollable {
    scrollbar-gutter: stable;
    flex: 1 1 1px;
    overflow-y: auto;
    overflow-x: hidden;
}

.backup-option:not(:hover) .backup-copy {
    visibility: hidden;
}

.tiny-button {
    font-size: 0.65rem;
    padding: 0.25rem;
    width: 2rem;
}
</style>

<style lang="scss" scoped>

</style>
