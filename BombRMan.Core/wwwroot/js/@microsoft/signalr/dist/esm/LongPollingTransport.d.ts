import { HttpClient } from "./HttpClient";
import { ILogger } from "./ILogger";
import { ITransport, TransferFormat } from "./ITransport";
import { IHttpConnectionOptions } from "./IHttpConnectionOptions";
/** @private */
export declare class LongPollingTransport implements ITransport {
    private readonly _httpClient;
    private readonly _accessTokenFactory;
    private readonly _logger;
    private readonly _options;
    private readonly _pollAbort;
    private _url?;
    private _running;
    private _receiving?;
    private _closeError?;
    onreceive: ((data: string | ArrayBuffer) => void) | null;
    onclose: ((error?: Error) => void) | null;
    get pollAborted(): boolean;
    constructor(httpClient: HttpClient, accessTokenFactory: (() => string | Promise<string>) | undefined, logger: ILogger, options: IHttpConnectionOptions);
    connect(url: string, transferFormat: TransferFormat): Promise<void>;
    private _getAccessToken;
    private _updateHeaderToken;
    private _poll;
    send(data: any): Promise<void>;
    stop(): Promise<void>;
    private _raiseOnClose;
}
