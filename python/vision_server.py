"""Python vision pipeline for the Robo Mechanic Arm demo.

This module starts a TCP server that receives compressed images from Unity, detects
red cubes using OpenCV and sends motion commands back to Unity. The commands are
simple strings: "movered" when a red cube is visible and "idle" when not.

Run the server before pressing play in Unity:

    python vision_server.py

Dependencies:
    - opencv-python
    - numpy

"""

from __future__ import annotations

import argparse
import socket
import struct
from dataclasses import dataclass

import cv2
import numpy as np

COMMAND_IDLE = "idle"
COMMAND_MOVE_RED = "movered"


@dataclass
class VisionServerConfig:
    host: str = "0.0.0.0"
    port: int = 5005
    min_red_area: float = 0.005
    display: bool = False


def read_exact(stream: socket.socket, length: int) -> bytes:
    """Read an exact number of bytes from the socket."""
    chunks = []
    bytes_recd = 0
    while bytes_recd < length:
        chunk = stream.recv(length - bytes_recd)
        if chunk == b"":
            raise ConnectionError("Socket connection broken")
        chunks.append(chunk)
        bytes_recd += len(chunk)
    return b"".join(chunks)


def decode_image(payload: bytes) -> np.ndarray:
    """Decode a JPEG/PNG byte payload into a BGR image."""
    data = np.frombuffer(payload, dtype=np.uint8)
    image = cv2.imdecode(data, cv2.IMREAD_COLOR)
    if image is None:
        raise ValueError("Unable to decode image from payload")
    return image


def detect_red_regions(frame: np.ndarray, min_red_ratio: float) -> tuple[bool, np.ndarray]:
    """Return whether a significant red region exists and a mask for visualisation."""
    hsv = cv2.cvtColor(frame, cv2.COLOR_BGR2HSV)

    lower_red_1 = np.array([0, 120, 70])
    upper_red_1 = np.array([10, 255, 255])
    lower_red_2 = np.array([170, 120, 70])
    upper_red_2 = np.array([180, 255, 255])

    mask1 = cv2.inRange(hsv, lower_red_1, upper_red_1)
    mask2 = cv2.inRange(hsv, lower_red_2, upper_red_2)
    mask = cv2.bitwise_or(mask1, mask2)

    mask = cv2.GaussianBlur(mask, (9, 9), 0)
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))

    red_ratio = float(np.count_nonzero(mask)) / mask.size
    return red_ratio >= min_red_ratio, mask


@dataclass
class VisionServer:
    config: VisionServerConfig

    def serve_forever(self) -> None:
        address = (self.config.host, self.config.port)
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server_socket:
            server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            server_socket.bind(address)
            server_socket.listen(1)
            print(f"Vision server listening on {address}")

            while True:
                client_socket, client_address = server_socket.accept()
                print(f"Client connected: {client_address}")
                try:
                    self.handle_client(client_socket)
                except Exception as exc:
                    print(f"Client error: {exc}")
                finally:
                    client_socket.close()
                    print("Client disconnected")

    def handle_client(self, client_socket: socket.socket) -> None:
        with client_socket:
            while True:
                length_bytes = client_socket.recv(4)
                if not length_bytes:
                    break
                (frame_length,) = struct.unpack("<I", length_bytes)
                payload = read_exact(client_socket, frame_length)
                frame = decode_image(payload)

                has_red, mask = detect_red_regions(frame, self.config.min_red_area)
                command = COMMAND_MOVE_RED if has_red else COMMAND_IDLE

                if self.config.display:
                    self.display_debug(frame, mask, command)

                self.send_command(client_socket, command)

    def display_debug(self, frame: np.ndarray, mask: np.ndarray, command: str) -> None:
        debug_frame = frame.copy()
        mask_colored = cv2.cvtColor(mask, cv2.COLOR_GRAY2BGR)
        debug_frame = cv2.addWeighted(debug_frame, 0.8, mask_colored, 0.2, 0)
        cv2.putText(debug_frame, f"Command: {command}", (16, 32), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 255, 0), 2)
        cv2.imshow("Vision", debug_frame)
        cv2.waitKey(1)

    def send_command(self, client_socket: socket.socket, command: str) -> None:
        payload = command.encode("utf-8")
        header = struct.pack("<I", len(payload))
        client_socket.sendall(header + payload)


def parse_args() -> VisionServerConfig:
    parser = argparse.ArgumentParser(description="Robo Mechanic Arm vision server")
    parser.add_argument("--host", default="0.0.0.0", help="Host/IP to bind the TCP server")
    parser.add_argument("--port", type=int, default=5005, help="Port for the TCP server")
    parser.add_argument("--min-red-area", type=float, default=0.005, help="Minimum red pixel ratio to trigger movement")
    parser.add_argument("--display", action="store_true", help="Display processed frames for debugging")
    args = parser.parse_args()
    return VisionServerConfig(host=args.host, port=args.port, min_red_area=args.min_red_area, display=args.display)


def main() -> None:
    config = parse_args()
    server = VisionServer(config)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("Stopping vision server...")
    finally:
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
