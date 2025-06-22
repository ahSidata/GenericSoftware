"""
Client for the grobro mqtt side, handling messages from/to
* growatt cloud
* growatt devices
"""

import os
import signal
import time
import struct
import logging
import ssl
from typing import Callable

import paho.mqtt.client as mqtt
from paho.mqtt.client import MQTTMessage

class SignalHandler:
    """
    Catches SIGINT and SIGTERM in order to trigger
    graceful shutdown.
    """

    _caught: bool

    def __init__(self):
        self._running = True
        # Register signal handlers for graceful shutdown
        signal.signal(signal.SIGINT, self._handle)
        signal.signal(signal.SIGTERM, self._handle)

    def _handle(self, _, __):
        """
        Handles signal by setting RUNNING to false.
        """
        LOG.info("Signal received, shutting down...")
        self._running = False

    @property
    def caught(self) -> bool:
        """
        Wether the signal was caught.
        """
        return self._running

class Client:

    _host = "mqtt.growatt.com"
    _port = 7006
    _clientId = "" 

    _client: mqtt.Client
    _forward_clients = {}

    def __init__(self):        
        self._client = mqtt.Client(
            client_id="grobro-grobro",
        )

        self._client.tls_set(cert_reqs=ssl.CERT_NONE)
        self._client.tls_insecure_set(True)

    def set_log_callback(self, log_callback=None):
        self.log_callback = log_callback
        
        if self.log_callback:
            self.log_callback("Logging initialized!")

    def set_dump_callback(self, dump_callback=None):
        self.dump_callback = dump_callback

    def set_options(self, options):
        if self.log_callback:
            self.log_callback(f"[TRACE] Python received options: Host={options.Host}, Port={options.Port}")
        self._host = options.Host
        self._port = options.Port

    def set_clientId(self, clientId):
        if self.log_callback:
            self.log_callback(f"[TRACE] Python received clientid: {clientId}")
        self._clientId = clientId

    def start(self):

        if self.log_callback:
            self.log_callback("Python client started!")

        self._client.connect("localhost", 7006, 60)
        self._client.on_message = self.__on_message
        
        if self.log_callback:
            self.log_callback("Subscribe c/#")

        self._client.subscribe("c/#")

        self._client.loop_start()

        signal_handler = SignalHandler()

        try:
            while signal_handler.caught:
                time.sleep(0.1)
        finally:
            if self.log_callback:
                self.log_callback("Stopped both clients. Exiting...")

    def stop(self):
        if self.log_callback:
            self.log_callback("GroBro: Stop")
        self._client.loop_stop()
        self._client.disconnect()
        for key, client in self._forward_clients.items():
            client.loop_stop()
            client.disconnect()

        if self.log_callback:
            self.log_callback("Python client finished!")

    def __on_message(self, client, userdata, msg: MQTTMessage):
        try:
            if self.dump_callback:
                self.dump_callback(msg.topic, msg.payload, msg.qos, msg.retain, msg.state, msg.dup, msg.mid)

            device_id = msg.topic.split("/")[-1]
            
            if self.log_callback:
                self.log_callback(f"Message from Broker, Topic: {msg.topic}, DeviceID: {device_id}")

            forward_client = self.__connect_to_growatt_server(device_id)
            forward_client.publish(
                msg.topic,
                payload=msg.payload,
                qos=msg.qos,
                retain=msg.retain,
            )
        except Exception as e:

            self.log_callback(f"Error Processing message: {e}")

    def __on_message_forward_client(self, client, userdata, msg: MQTTMessage):
        try:
            if self.dump_callback:
                self.dump_callback(msg.topic, msg.payload, msg.qos, msg.retain, msg.state, msg.dup, msg.mid)

            device_id = msg.topic.split("/")[-1]

            if self.log_callback:
                self.log_callback(f"Message from Growatt, Topic: {msg.topic}, DeviceID: {device_id}")

            # We need to publish the messages from Growatt on the Topic
            # s/33/{deviceid}. Growatt sends them on Topic s/{deviceid}
            self._client.publish(
                msg.topic.split("/")[0] + "/33/" + device_id,
                payload=msg.payload,
                qos=msg.qos,
                retain=msg.retain,
            )
        except Exception as e:
            if self.log_callback:
                self.log_callback(f"Forwarding message: {e}")

    # Setup Growatt MQTT broker for forwarding messages
    def __connect_to_growatt_server(self, client_id):
        if f"forward_client_{client_id}" not in self._forward_clients:
            if self.log_callback:
                self.log_callback("Connecting to Growatt broker at 'mqtt.growatt.com:7006'")
            client = mqtt.Client(
                client_id=client_id,
            )
            client.tls_set(cert_reqs=ssl.CERT_NONE)
            client.tls_insecure_set(True)
            client.on_message = self.__on_message_forward_client
            client.connect(
                "mqtt.growatt.com",
                7006,
                60,
            )

            client.loop_start()
            self._forward_clients[f"forward_client_{client_id}"] = client
        return self._forward_clients[f"forward_client_{client_id}"]


