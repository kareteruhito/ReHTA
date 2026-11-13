(() => {
    "use strict";

    const pending = new Map();
    let nextId = 1;

    // C#からの応答を受け取る
    window.chrome.webview.addEventListener(
        "message",
        event => {
            const message = event.data;

            if (!message ||
                typeof message.id !== "number") {
                return;
            }

            const request = pending.get(message.id);

            if (!request) {
                return;
            }

            pending.delete(message.id);

            if (message.success) {
                request.resolve(message.result);
            } else {
                request.reject(
                    new Error(
                        message.error ?? "不明なエラー"
                    )
                );
            }
        }
    );

    // C#に処理を依頼する共通関数
    function invoke(command, parameters = {}) {
        return new Promise((resolve, reject) => {
            const id = nextId++;

            pending.set(id, { resolve, reject });

            try {
                window.chrome.webview.postMessage({
                    id,
                    command,
                    ...parameters
                });
            } catch (error) {
                pending.delete(id);
                reject(error);
            }
        });
    }

    // ReHTA API
    const rehta = {
        writeText(filename, text) {
            return invoke("writeText", {
                filename,
                text
            });
        },

        readText(filename) {
            return invoke("readText", {
                filename
            });
        },

        sql(query, parameters = []) {
            return invoke("sql", { query, parameters });
        },
        ps(script) {
            return invoke("ps", { script });
        }
    };

    // グローバルAPIとして公開
    Object.defineProperty(window, "rehta", {
        value: Object.freeze(rehta),
        writable: false,
        configurable: false
    });
})();