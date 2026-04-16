/**
 * indexed-db.js
 * Browser IndexedDB wrapper — persistent model segment storage.
 * Called from C# IndexedDbInterop via JS module import.
 * UAC 7.3 (one-time provisioning, cached across sessions)
 */

const _DB_VERSION = 1;

function _openDb(dbName, storeName) {
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(dbName, _DB_VERSION);
        req.onupgradeneeded = e => {
            const db = e.target.result;
            if (!db.objectStoreNames.contains(storeName))
                db.createObjectStore(storeName);
        };
        req.onsuccess = e => resolve(e.target.result);
        req.onerror   = e => reject(e.target.error);
    });
}

export async function put(dbName, storeName, key, data) {
    const db = await _openDb(dbName, storeName);
    return new Promise((resolve, reject) => {
        const tx    = db.transaction(storeName, 'readwrite');
        const store = tx.objectStore(storeName);
        const req   = store.put(data, key);
        req.onsuccess = () => resolve();
        req.onerror   = () => reject(req.error);
        tx.oncomplete = () => db.close();
    });
}

export async function get(dbName, storeName, key) {
    const db = await _openDb(dbName, storeName);
    return new Promise((resolve, reject) => {
        const tx    = db.transaction(storeName, 'readonly');
        const store = tx.objectStore(storeName);
        const req   = store.get(key);
        req.onsuccess = () => resolve(req.result ?? null);
        req.onerror   = () => reject(req.error);
        tx.oncomplete = () => db.close();
    });
}

export async function exists(dbName, storeName, key) {
    const db = await _openDb(dbName, storeName);
    return new Promise((resolve, reject) => {
        const tx    = db.transaction(storeName, 'readonly');
        const store = tx.objectStore(storeName);
        const req   = store.count(key);
        req.onsuccess = () => resolve(req.result > 0);
        req.onerror   = () => reject(req.error);
        tx.oncomplete = () => db.close();
    });
}

export async function remove(dbName, storeName, key) {
    const db = await _openDb(dbName, storeName);
    return new Promise((resolve, reject) => {
        const tx    = db.transaction(storeName, 'readwrite');
        const store = tx.objectStore(storeName);
        const req   = store.delete(key);
        req.onsuccess = () => resolve();
        req.onerror   = () => reject(req.error);
        tx.oncomplete = () => db.close();
    });
}

export async function clear(dbName, storeName) {
    const db = await _openDb(dbName, storeName);
    return new Promise((resolve, reject) => {
        const tx    = db.transaction(storeName, 'readwrite');
        const store = tx.objectStore(storeName);
        const req   = store.clear();
        req.onsuccess = () => resolve();
        req.onerror   = () => reject(req.error);
        tx.oncomplete = () => db.close();
    });
}

export async function getStoreSize(dbName, storeName) {
    const db = await _openDb(dbName, storeName);
    return new Promise((resolve, reject) => {
        const tx    = db.transaction(storeName, 'readonly');
        const store = tx.objectStore(storeName);
        let totalSize = 0;
        const cursor = store.openCursor();
        cursor.onsuccess = e => {
            const c = e.target.result;
            if (c) {
                const val = c.value;
                if (val instanceof ArrayBuffer) totalSize += val.byteLength;
                else if (val?.byteLength)        totalSize += val.byteLength;
                c.continue();
            } else {
                resolve(totalSize);
            }
        };
        cursor.onerror = () => reject(cursor.error);
        tx.oncomplete  = () => db.close();
    });
}
